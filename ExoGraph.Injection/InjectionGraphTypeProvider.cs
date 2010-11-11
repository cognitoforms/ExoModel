using System;
using System.Collections.Generic;
using System.Reflection;
using PostSharp.Laos;
using System.ComponentModel;

namespace ExoGraph.Injection
{
	/// <summary>
	/// Implementation of <see cref="GraphContext"/> that injects logic into compiled
	/// assemblies to monitor graph changes.
	/// </summary>
	public abstract class InjectionGraphTypeProvider : ReflectionGraphTypeProvider
	{
		public InjectionGraphTypeProvider(IEnumerable<Type> types)
			: base("", types)
		{ }

		public InjectionGraphTypeProvider(string @namespace, IEnumerable<Type> types)
			: base(@namespace, types, null)
		{ }

		public InjectionGraphTypeProvider(params Assembly[] assemblies)
			: base("", GetTypes(assemblies), null)
		{ }

		public InjectionGraphTypeProvider(string @namespace, params Assembly[] assemblies)
			: base(@namespace, GetTypes(assemblies), null)
		{ }

		static IEnumerable<Type> GetTypes(params Assembly[] assemblies)
		{
			foreach (Assembly assembly in assemblies)
				foreach (Type type in assembly.GetTypes())
					if (typeof(IGraphInstance).IsAssignableFrom(type))
						yield return type;
		}

		internal static CompositionAspect GetInstanceAspect()
		{
			return new InjectionGraphType.InstanceAspect();
		}

		internal static OnMethodBoundaryAspect GetOnPropertyGetAspect(PropertyInfo property)
		{
			return new InjectionGraphType.OnPropertyGetAspect(property);
		}

		internal static OnMethodBoundaryAspect GetOnPropertySetAspect(PropertyInfo property)
		{
			return new InjectionGraphType.OnPropertySetAspect(property);
		}

		#region InjectionGraphType

		public abstract class InjectionGraphType : ReflectionGraphType
		{
			protected InjectionGraphType(string @namespace, Type type)
				: base(@namespace, type)
			{ }

			public override GraphInstance GetGraphInstance(object instance)
			{
				var gi = instance as IGraphInstance;
				return gi == null ? null : gi.Instance;
			}

			#region InstanceAspect

			/// <summary>
			/// Aspect that supports storage of a <see cref="GraphInstance"/> on behalf of a real graph instance.
			/// </summary>
			[Serializable]
			public class InstanceAspect : CompositionAspect
			{
				/// <summary>
				/// Called at runtime, creates the implementation of the <see cref="INotifyPropertyChanged"/> interface.
				/// </summary>
				/// <param name="eventArgs">Execution context.</param>
				/// <returns>A new instance of <see cref="NotifyPropertyChangedImplementation"/>, which implements
				/// <see cref="INotifyPropertyChanged"/>.</returns>
				public override object CreateImplementationObject(InstanceBoundLaosEventArgs eventArgs)
				{
					return new InstanceTracker(eventArgs.Instance);
				}

				public override Type GetPublicInterface(Type containerType)
				{
					return typeof(IGraphInstance);
				}

				/// <summary>
				/// Gets weaving options specifying that the implementation accessor interface (<see cref="IComposed{T}"/>)
				/// should be exposed, and that the implementation of interfaces should be silently ignored if they are
				/// already implemented in the parent types.</returns>
				public override CompositionAspectOptions GetOptions()
				{
					return
						 CompositionAspectOptions.GenerateImplementationAccessor |
						 CompositionAspectOptions.IgnoreIfAlreadyImplemented;
				}

				#region InstanceTracker

				/// <summary>
				/// Implementation of <see cref="IGraphInstance"/> that tracks the
				/// <see cref="GraphInstance"/> on behalf of the real instance.
				/// </summary>
				[Serializable]
				public class InstanceTracker : IGraphInstance
				{
					GraphInstance instance;

					public InstanceTracker(object instance)
					{
						this.instance = InjectionGraphTypeProvider.CreateGraphInstance(instance);
					}

					public GraphInstance Instance
					{
						get
						{
							return instance;
						}
					}
				}

				#endregion
			}

			#endregion

			#region OnPropertyGetAspect

			/// <summary>
			/// Implementation of <see cref="OnMethodBoundaryAspect"/> that notifies the context when
			/// properties on the underlying instance are fetched.
			/// </summary>
			[Serializable]
			public class OnPropertyGetAspect : OnMethodBoundaryAspect
			{
				readonly string property;

				/// <summary>
				/// Initializes a new <see cref="OnPropertyGetAspect"/>.
				/// </summary>
				/// <param name="property">The property this aspect is for</param>
				public OnPropertyGetAspect(PropertyInfo property)
				{
					this.property = property.Name;
				}

				/// <summary>
				/// Notifies the <see cref="GraphContext"/> that a property is being fetched.
				/// </summary>
				/// <param name="eventArgs">Information about the current execution context.</param>
				public override void OnEntry(MethodExecutionEventArgs eventArgs)
				{
					GraphInstance instance = ((IGraphInstance) eventArgs.Instance).Instance;
					GraphProperty property = instance.Type.Properties[this.property];
					if (property != null)
						((InjectionGraphType)property.DeclaringType).OnPropertyGet(instance, property);
				}
			}

			#endregion

			#region OnPropertySetAspect

			/// <summary>
			/// Implementation of <see cref="OnMethodBoundaryAspect"/> that notifies the context when
			/// properties on the underlying instance are fetched or changed.
			/// </summary>
			[Serializable]
			public class OnPropertySetAspect : OnMethodBoundaryAspect
			{
				readonly string property;

				/// <summary>
				/// Initializes a new <see cref="OnPropertySetAspect"/>.
				/// </summary>
				/// <param name="property">The property to which this aspect is for.</param>
				public OnPropertySetAspect(PropertyInfo property)
				{
					this.property = property.Name;
				}

				public override void OnEntry(MethodExecutionEventArgs eventArgs)
				{
					// Get the graph instance associated with the current object
					GraphInstance instance = ((IGraphInstance) eventArgs.Instance).Instance;

					// Exit immediately if the property is not valid for the current graph type
					if (!instance.Type.Properties.Contains(property))
						return;

					// Store the current property value as a method execution tag
					eventArgs.MethodExecutionTag = instance[this.property];

					// Call the base class implementation
					base.OnEntry(eventArgs);
				}

				/// <summary>
				/// Executed when the set accessor successfully completes. Raises the 
				/// <see cref="INotifyPropertyChanged.PropertyChanged"/> event.
				/// </summary>
				/// <param name="eventArgs">Event arguments with information about the 
				/// current execution context.</param>
				public override void OnSuccess(MethodExecutionEventArgs eventArgs)
				{
					// Get the graph instance associated with the current object
					GraphInstance instance = ((IGraphInstance) eventArgs.Instance).Instance;

					GraphProperty graphProperty = instance.Type.Properties[property];

					// Exit immediately if the property is not valid for the current graph type
					if (graphProperty == null)
						return;

					object originalValue = eventArgs.MethodExecutionTag;
					object currentValue = instance[property];

					// Raise property change if the current value is different from the original value
					if ((originalValue == null ^ currentValue == null) || (originalValue != null && !originalValue.Equals(currentValue)))
						graphProperty.OnPropertyChanged(instance, originalValue, currentValue);
				}
			}

			#endregion
		}

		#endregion
	}

	#region IGraphInstance

	public interface IGraphInstance
	{
		GraphInstance Instance
		{
			get;
		}
	}

	#endregion
}
