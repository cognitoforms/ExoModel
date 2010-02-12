using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using NHibernate;
using Castle.DynamicProxy;
using Castle.Core.Interceptor;

namespace ExoGraph.NHibernate
{
	/// <summary>
	/// Factory class for new entity creation
	/// </summary>
	/// <remarks>
	/// Proxies entities so that they may be tied-in to the ExoGraph entity lifecycle
	/// </remarks>
	public class DataBindingFactory
	{
		private static readonly ProxyGenerator ProxyGenerator = new ProxyGenerator();

		public static T Create<T>()
		{
			return (T)Create(typeof(T));
		}

		/// <summary>
		/// Creates a new instance of a proxied entity
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object Create(Type type)
		{
			var options = new ProxyGenerationOptions();
			options.AddMixinInstance(new InstanceTracker());

			var result = ProxyGenerator.CreateClassProxy(type, new[]
			{
			    typeof(INotifyPropertyModified),
				typeof(INotifyPropertyAccessed),
			    typeof(IMarkerInterface)
			}, options, new NotifyPropertyChangedInterceptor(type.Name));

			((IGraphInstance) result).Instance = ((NHibernateGraphContext) GraphContext.Current).InitGraphInstance(result);

			((INotifyPropertyAccessed) result).PropertyAccessed += new PropertyAccessedEventHandler(((NHibernateGraphContext) GraphContext.Current).NHibernateGraphContext_PropertyAccessed);
			((INotifyPropertyModified) result).PropertyModified += new PropertyModifiedEventHandler(((NHibernateGraphContext) GraphContext.Current).NHibernateGraphContext_PropertyModified);

			return result;
		}

		#region IMarkerInterface

		/// <summary>
		/// Identifies an entity as a proxied entity
		/// </summary>
		public interface IMarkerInterface
		{
			string TypeName { get; }
		}

		#endregion

		#region IGraphInstance

		/// <summary>
		/// Identifies an entity as a <see cref="ExoGraph.GraphInstance"/> provider
		/// </summary>
		public interface IGraphInstance
		{
			GraphInstance Instance { get; set; }
		}

		#endregion

		#region InstanceTracker

		/// <summary>
		/// Binds a <see cref="ExoGraph.GraphInstance"/> to an existing entity
		/// </summary>
		private class InstanceTracker : IGraphInstance
		{
			GraphInstance instance;

			public InstanceTracker()
			{
			}

			public void SetInstance(object instance)
			{
				this.instance = ((NHibernateGraphContext)GraphContext.Current).InitGraphInstance(instance);
			}

			public GraphInstance Instance
			{
				get
				{
					return instance;
				}
				set
				{
					instance = value;
				}
			}
		}

		#endregion

		#region NotifyPropertyChangedInterceptor

		/// <summary>
		/// Intercepts all method calls to the proxied entity
		/// </summary>
		public class NotifyPropertyChangedInterceptor : Castle.Core.Interceptor.IInterceptor
		{
			private readonly string typeName;
			private PropertyModifiedEventHandler modifySubscribers = delegate { };
			private PropertyAccessedEventHandler accessSubscribers = delegate { };
			private object oldValue;
			private object newValue;

			public NotifyPropertyChangedInterceptor(string typeName)
			{
				this.typeName = typeName;
			}

			public void Intercept(IInvocation invocation)
			{
				if (invocation.Method.DeclaringType == typeof(IMarkerInterface))
				{
					invocation.ReturnValue = typeName;
					return;
				}

				// If adding a PropertyModifiedEventHandler, intercept and add
				if (invocation.Method.DeclaringType == typeof(INotifyPropertyModified))
				{
					var propertyModifiedEventHandler = (PropertyModifiedEventHandler) invocation.Arguments[0];
					if (invocation.Method.Name.StartsWith("add_"))
						modifySubscribers += propertyModifiedEventHandler;
					else
						modifySubscribers -= propertyModifiedEventHandler;

					return;
				}

				// If adding a PropertyAccessedEventHandler, intercept and add
				if (invocation.Method.DeclaringType == typeof(INotifyPropertyAccessed))
				{
					var propertyAccessedEventHandler = (PropertyAccessedEventHandler) invocation.Arguments[0];
					if (invocation.Method.Name.StartsWith("add_"))
						accessSubscribers += propertyAccessedEventHandler;
					else
						accessSubscribers -= propertyAccessedEventHandler;

					return;
				}

				// Invoke accessed event handler before value is fetched
				if (invocation.Method.DeclaringType != typeof(IGraphInstance) && invocation.Method.Name.StartsWith("get_"))
				{
					var propertyName = invocation.Method.Name.Substring(4);
					accessSubscribers(invocation.InvocationTarget, new PropertyAccessedEventArgs(propertyName));
				}

				// Save before and [possible] after values if this is a 'set' operation
				if (invocation.Method.DeclaringType != typeof(IGraphInstance) && invocation.Method.Name.StartsWith("set_"))
				{
					// This will only work on an IGraphInstance, and this may be occuring during construction
					//TODO: ensure this works when setting a value to null
					if (((IGraphInstance)invocation.InvocationTarget).Instance != null)
					{
						string propertyName = invocation.Method.Name.Substring(4);
						newValue = invocation.Arguments[0];
						oldValue = ((IGraphInstance) invocation.InvocationTarget).Instance[propertyName];
					}
				}

				invocation.Proceed();

				// Invoke event handler after value has been set
				if (invocation.Method.DeclaringType != typeof(IGraphInstance) && invocation.Method.Name.StartsWith("set_"))
				{
					if (oldValue != newValue)
					{
						var propertyName = invocation.Method.Name.Substring(4);
						modifySubscribers(invocation.InvocationTarget, new PropertyModifiedEventArgs(propertyName, oldValue, newValue));
					}
				}
			}
		}

		#endregion
	}
}
