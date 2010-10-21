using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace ExoGraph
{
	/// <summary>
	/// Base class for type providers that expose properties dynamically but leverage base type providers
	/// for core functionality.
	/// </summary>
	/// <typeparam name="TTypeSource"></typeparam>
	/// <typeparam name="TPropertySource"></typeparam>
	public abstract class DynamicGraphTypeProvider<TTypeSource, TPropertySource> : IGraphTypeProvider
	{
		#region Fields

		string @namespace;
		string baseType;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="DynamicGraphTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="namespace"></param>
		/// <param name="create"></param>
		public DynamicGraphTypeProvider(string @namespace, string baseType)
		{
			this.@namespace = string.IsNullOrEmpty(@namespace) ? string.Empty : @namespace + ".";
			this.baseType = baseType;
		}

		#endregion

		#region IGraphTypeProvider

		/// <summary>
		/// Gets the unique name of the <see cref="GraphType"/> for the specified graph object instance.
		/// </summary>
		/// <param name="instance">The actual graph object instance</param>
		/// <returns>The unique name of the graph type for the instance if it is a valid graph type, otherwise null</returns>
		string IGraphTypeProvider.GetGraphTypeName(object instance)
		{
			TTypeSource type = GetTypeSource(instance);
			return type == null ? null : @namespace + GetTypeName(type);
		}

		/// <summary>
		/// Gets the <see cref="GraphType"/> that corresponds to the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		string IGraphTypeProvider.GetGraphTypeName(Type type)
		{
			// Return null to indicate that the current type provider does not support concrete types
			return null;
		}

		/// <summary>
		/// Creates a <see cref="GraphType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		GraphType IGraphTypeProvider.CreateGraphType(string typeName)
		{
			// Exit immediately if the requested type is from a different namespace.
			if (!typeName.StartsWith(@namespace))
				return null;
			
			// Remove the namespace
			string className = typeName.Substring(@namespace.Length);
			
			// See if a type source is available for the specified type name
			TTypeSource type = GetTypeSource(className);
			if (type == null)
				return null;

			// Return a new dynamic graph type
			return new DynamicGraphType(typeName, GetBaseType(), GetTypeAttributes(type), GetProperties(type));
		}

		#endregion

		#region Methods

		internal virtual GraphType GetBaseType()
		{
			return GraphContext.Current.GetGraphType(baseType);
		}

		protected abstract object CreateInstance(TTypeSource type);

		protected abstract IEnumerable<TPropertySource> GetProperties(TTypeSource type);

		protected abstract TTypeSource GetTypeSource(string typeName);

		protected abstract TTypeSource GetTypeSource(object instance);

		protected abstract string GetTypeName(TTypeSource type);

		protected abstract Attribute[] GetTypeAttributes(TTypeSource type);

		protected abstract string GetPropertyName(TPropertySource property);

		protected abstract Attribute[] GetPropertyAttributes(TPropertySource property);

		protected abstract bool IsList(TPropertySource property);

		protected abstract GraphType GetReferenceType(TPropertySource property);

		protected abstract TypeConverter GetValueConverter(TPropertySource property);

		protected abstract Type GetValueType(TPropertySource property);

		protected abstract object GetPropertyValue(object instance, TPropertySource property);

		protected abstract void SetPropertyValue(object instance, TPropertySource property, object value);

		protected virtual void OnCreateGraphType(GraphType type)
		{ }

		#endregion

		#region DynamicGraphType

		class DynamicGraphType : GraphType
		{
			IEnumerable<TPropertySource> properties;

			internal DynamicGraphType(string name, GraphType baseType, Attribute[] attributes, IEnumerable<TPropertySource> properties)
				: base(name, baseType.QualifiedName + "+" + name, baseType, attributes) 
			{
				this.properties = properties;
			}

			protected internal override void OnInit()
			{
				// Get the type provider
				var provider = (DynamicGraphTypeProvider<TTypeSource, TPropertySource>)Provider;

				// Automatically inherit all base type properties
				foreach (var property in BaseType.Properties)
					AddProperty(property);

				// Create graph properties for each source property
				foreach (TPropertySource property in properties)
				{
					// Get the name of the property
					var name = provider.GetPropertyName(property);

					// Skip this property if it has already been added
					if (Properties.Contains(name))
						continue;

					// Determine if the property is a list
					var isList = provider.IsList(property);

					// Get the attributes for the property
					var attributes = provider.GetPropertyAttributes(property);

					// Determine whether the property is a reference or value type
					var referenceType	= provider.GetReferenceType(property);

					// Add the new value or reference property
					if (referenceType == null)
						AddProperty(
							new DynamicGraphTypeProvider<TTypeSource, TPropertySource>.DynamicValueProperty(
								this, property, name, provider.GetValueType(property), provider.GetValueConverter(property), isList, attributes)
						);
					else
						AddProperty(
							new DynamicGraphTypeProvider<TTypeSource, TPropertySource>.DynamicReferenceProperty(
								this, property, name, referenceType, isList, attributes)
						);
					
				}

				// Remove the reference to the property source to avoid caching instance data with the type
				this.properties = null;

				// Notify provider subclasses that the graph type has been created
				provider.OnCreateGraphType(this);
			}

			protected internal override System.Collections.IList ConvertToList(GraphReferenceProperty property, object list)
			{
				return BaseType.ConvertToList(property, list);
			}

			protected internal override void SaveInstance(GraphInstance graphInstance)
			{
				BaseType.SaveInstance(graphInstance);
			}

			public override GraphInstance GetGraphInstance(object instance)
			{
				return BaseType.GetGraphInstance(instance);
			}

			protected internal override string GetId(object instance)
			{
				return BaseType.GetId(instance);
			}

			protected internal override object GetInstance(string id)
			{
				if (id == null)
				{
					var provider = (DynamicGraphTypeProvider<TTypeSource, TPropertySource>)Provider;
					return provider.CreateInstance(provider.GetTypeSource(Name));
				}
				else
					return BaseType.GetInstance(id);
			}

			protected internal override void DeleteInstance(GraphInstance graphInstance)
			{
				BaseType.DeleteInstance(graphInstance);
			}
		}

		#endregion

		#region DescriptorValueProperty

		[Serializable]
		class DynamicValueProperty : GraphValueProperty
		{
			internal DynamicValueProperty(DynamicGraphType declaringType, TPropertySource property, string name, Type propertyType, TypeConverter converter, bool isList, Attribute[] attributes)
				: base(declaringType, name, false, propertyType, converter, isList, attributes)
			{
				this.PropertySource = property;
			}

			protected internal TPropertySource PropertySource { get; private set; }

			protected internal override object GetValue(object instance)
			{
				return ((DynamicGraphTypeProvider<TTypeSource, TPropertySource>)((DynamicGraphType)DeclaringType).Provider)
					.GetPropertyValue(instance, PropertySource);
			}

			protected internal override void SetValue(object instance, object value)
			{
				object originalValue = GetValue(instance);
				if ((originalValue == null ^ value == null) || (originalValue != null && !originalValue.Equals(value)))
				{
					((DynamicGraphTypeProvider<TTypeSource, TPropertySource>)((DynamicGraphType)DeclaringType).Provider)
						.SetPropertyValue(instance, PropertySource, value);
					OnPropertyChanged(DeclaringType.GetGraphInstance(instance), originalValue, value);
				}
			}
		}

		#endregion

		#region DynamicReferenceProperty

		[Serializable]
		class DynamicReferenceProperty : GraphReferenceProperty
		{
			internal DynamicReferenceProperty(DynamicGraphType declaringType, TPropertySource property, string name, GraphType propertyType, bool isList, Attribute[] attributes)
				: base(declaringType, name, false, false, propertyType, isList, attributes)
			{
				this.PropertySource = property;
			}

			protected internal TPropertySource PropertySource { get; private set; }

			protected internal override object GetValue(object instance)
			{
				return ((DynamicGraphTypeProvider<TTypeSource, TPropertySource>)((DynamicGraphType)DeclaringType).Provider)
					.GetPropertyValue(instance, PropertySource);
			}

			protected internal override void SetValue(object instance, object value)
			{
				((DynamicGraphTypeProvider<TTypeSource, TPropertySource>)((DynamicGraphType)DeclaringType).Provider)
					.SetPropertyValue(instance, PropertySource, value);
			}
		}

		#endregion
	}

	/// <summary>
	/// Base class for type providers that expose properties dynamically but leverage base type providers
	/// for core functionality.
	/// </summary>
	/// <typeparam name="TBaseType"></typeparam>
	/// <typeparam name="TTypeSource"></typeparam>
	/// <typeparam name="TPropertySource"></typeparam>
	public abstract class DynamicGraphTypeProvider<TBaseType, TTypeSource, TPropertySource> : DynamicGraphTypeProvider<TTypeSource, TPropertySource>
	{
		protected DynamicGraphTypeProvider(string @namespace)
			: base(@namespace, null)
		{ }

		internal override GraphType  GetBaseType()
		{
			 return GraphContext.Current.GetGraphType<TBaseType>();
		}

		protected abstract TBaseType Create(TTypeSource type);

		protected override object CreateInstance(TTypeSource type)
		{
			return Create(type);
		}

		protected abstract object GetPropertyValue(TBaseType instance, TPropertySource property);

		protected override object GetPropertyValue(object instance, TPropertySource property)
		{
			return GetPropertyValue((TBaseType)instance, property);
		}

		protected abstract void SetPropertyValue(TBaseType instance, TPropertySource property, object value);

		protected override void SetPropertyValue(object instance, TPropertySource property, object value)
		{
			SetPropertyValue((TBaseType)instance, property, value);
		}

		protected abstract TTypeSource GetTypeSource(TBaseType instance);

		protected override TTypeSource GetTypeSource(object instance)
		{
			return GetTypeSource((TBaseType)instance);
		}
	}
}
