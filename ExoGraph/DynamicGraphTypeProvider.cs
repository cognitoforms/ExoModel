using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ExoGraph
{
	#region DynamicGraphTypeProvider

	/// <summary>
	/// Base class for type providers that expose properties dynamically but leverage base type providers
	/// for core functionality.
	/// </summary>
	/// <typeparam name="TTypeSource"></typeparam>
	/// <typeparam name="TPropertySource"></typeparam>
	public abstract class DynamicGraphTypeProvider : IGraphTypeProvider
	{
		#region Fields

		string @namespace;
		string baseType;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="DynamicGraphTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="@namespace"></param>
		/// <param name="baseType"></param>
		internal DynamicGraphTypeProvider(string @namespace, string baseType)
		{
			this.@namespace = string.IsNullOrEmpty(@namespace) ? string.Empty : @namespace + ".";
			this.baseType = baseType;
		}

		#endregion

		#region Properties

		protected string Namespace
		{
			get { return @namespace; }
		}

		protected string BaseType
		{
			get { return baseType; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Gets the unique name of the <see cref="GraphType"/> for the specified graph object instance.
		/// </summary>
		/// <param name="instance">The actual graph object instance</param>
		/// <returns>The unique name of the graph type for the instance if it is a valid graph type, otherwise null</returns>
		protected abstract string GetGraphTypeName(object instance);

		/// <summary>
		/// Creates a <see cref="GraphType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		protected abstract GraphType CreateGraphType(string typeName);

		#endregion

		#region IGraphTypeProvider

		/// <summary>
		/// Gets the unique name of the <see cref="GraphType"/> for the specified graph object instance.
		/// </summary>
		/// <param name="instance">The actual graph object instance</param>
		/// <returns>The unique name of the graph type for the instance if it is a valid graph type, otherwise null</returns>
		string IGraphTypeProvider.GetGraphTypeName(object instance)
		{
			return GetGraphTypeName(instance);
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
			return CreateGraphType(typeName);
		}

		#endregion
	}

	#endregion

	#region DynamicGraphTypeProvider<TTypeSource, TPropertySource>

	/// <summary>
	/// Base class for type providers that expose properties dynamically but leverage base type providers
	/// for core functionality.
	/// </summary>
	/// <typeparam name="TTypeSource"></typeparam>
	/// <typeparam name="TPropertySource"></typeparam>
	public abstract class DynamicGraphTypeProvider<TTypeSource, TPropertySource> : DynamicGraphTypeProvider
        where TTypeSource : class
	{
		#region Constructors

		/// <summary>
		/// Creates a new <see cref="DynamicGraphTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="@namespace"></param>
		/// <param name="baseType"></param>
		public DynamicGraphTypeProvider(string @namespace, string baseType)
			: base(@namespace, baseType)
		{
		}

		#endregion

		#region Methods

		/// <summary>
		/// Gets the unique name of the <see cref="GraphType"/> for the specified graph object instance.
		/// </summary>
		/// <param name="instance">The actual graph object instance</param>
		/// <returns>The unique name of the graph type for the instance if it is a valid graph type, otherwise null</returns>
		protected override string GetGraphTypeName(object instance)
		{
			TTypeSource type = GetTypeSource(instance);
			return type == null ? null : GetTypeName(type);
		}

		/// <summary>
		/// Creates a <see cref="GraphType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		protected override GraphType CreateGraphType(string typeName)
		{
			// Exit immediately if the requested type is from a different namespace.
			if (!typeName.StartsWith(Namespace))
				return null;

			// See if a type source is available for the specified type name
			TTypeSource type = GetTypeSource(typeName);
			if (type == null)
				return null;

			// Return a new dynamic graph type
			return new DynamicGraphType(typeName, GetBaseType(), GetTypeAttributes(type), GetProperties(type));
		}

		internal virtual GraphType GetBaseType()
		{
			return GraphContext.Current.GetGraphType(BaseType);
		}

		protected internal string GetTypeName(TTypeSource type)
		{
			return Namespace + GetClassName(type);
		}

		protected abstract object CreateInstance(TTypeSource type);

		protected abstract IEnumerable<TPropertySource> GetProperties(TTypeSource type);

		/// <summary>
		/// Gets the real object that provides type information based on the given dynamic type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		protected abstract TTypeSource GetTypeSource(string typeName);

		protected abstract TTypeSource GetTypeSource(object instance);

		protected abstract string GetClassName(TTypeSource type);

		protected abstract Attribute[] GetTypeAttributes(TTypeSource type);

		protected abstract string GetPropertyName(TPropertySource property);

		protected abstract Attribute[] GetPropertyAttributes(TPropertySource property);

		protected abstract bool IsList(TPropertySource property);

		protected abstract bool IsReadOnly(TPropertySource property);

		protected abstract GraphType GetReferenceType(TPropertySource property);

		protected abstract TypeConverter GetValueConverter(TPropertySource property);

		protected abstract Type GetValueType(TPropertySource property);

		protected abstract object GetPropertyValue(object instance, TPropertySource property);

		protected abstract void SetPropertyValue(object instance, TPropertySource property, object value);

		protected virtual void OnCreateGraphType(GraphType type)
		{ }

		#endregion

		#region DynamicGraphType

		[Serializable]
		class DynamicGraphType : GraphType
		{
			IEnumerable<TPropertySource> properties;

			internal DynamicGraphType(string name, GraphType baseType, Attribute[] attributes, IEnumerable<TPropertySource> properties)
				: base(name, baseType.QualifiedName + "+" + name, baseType, baseType.Scope, attributes)
			{
				this.properties = properties;
			}

			protected internal new DynamicGraphTypeProvider<TTypeSource, TPropertySource> Provider
			{
				get
				{
					return base.Provider as DynamicGraphTypeProvider<TTypeSource, TPropertySource>;
				}
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

					// Determine if the property is read only
					var isReadOnly = provider.IsReadOnly(property);

					// Get the attributes for the property
					var attributes = provider.GetPropertyAttributes(property);

					// Determine whether the property is a reference or value type
					var referenceType = provider.GetReferenceType(property);

					// Add the new value or reference property
					if (referenceType == null)
						AddProperty(
							new DynamicValueProperty(
								this, property, name, provider.GetValueType(property), provider.GetValueConverter(property), isList, isReadOnly, attributes)
						);
					else
						AddProperty(
							new DynamicReferenceProperty(
								this, property, name, referenceType, isList, isReadOnly, attributes)
						);

				}

				// Remove the reference to the property source to avoid caching instance data with the type
				this.properties = null;

				// Notify provider subclasses that the graph type has been created
				provider.OnCreateGraphType(this);
			}

			protected internal override IList ConvertToList(GraphReferenceProperty property, object list)
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
					return Provider.CreateInstance(Provider.GetTypeSource(Name));
				else
					return BaseType.GetInstance(id);
			}

			protected internal override void DeleteInstance(GraphInstance graphInstance)
			{
				BaseType.DeleteInstance(graphInstance);
			}

			protected internal override void OnStartTrackingList(GraphInstance instance, GraphReferenceProperty property, IList list)
			{
				Provider.GetBaseType().OnStartTrackingList(instance, property, list);
			}

			protected internal override void OnStopTrackingList(GraphInstance instance, GraphReferenceProperty property, IList list)
			{
				Provider.GetBaseType().OnStopTrackingList(instance, property, list);
			}

			#region DescriptorValueProperty

			[Serializable]
			class DynamicValueProperty : GraphValueProperty
			{
				internal DynamicValueProperty(DynamicGraphType declaringType, TPropertySource property, string name, Type propertyType, TypeConverter converter, bool isList, bool isReadOnly, Attribute[] attributes)
					: base(declaringType, name, false, propertyType, converter, isList, isReadOnly, attributes)
				{
					this.PropertySource = property;
				}

				protected internal TPropertySource PropertySource { get; private set; }

				public new DynamicGraphType DeclaringType { get { return (DynamicGraphType)base.DeclaringType; } }

				protected internal override object GetValue(object instance)
				{
					DeclaringType.GetGraphInstance(instance).OnPropertyGet(this);
					return DeclaringType.Provider.GetPropertyValue(instance, PropertySource);
				}

				protected internal override void SetValue(object instance, object value)
				{
					object originalValue = DeclaringType.Provider.GetPropertyValue(instance, PropertySource);

					if ((originalValue == null ^ value == null) || (originalValue != null && !originalValue.Equals(value)))
					{
						DeclaringType.Provider.SetPropertyValue(instance, PropertySource, value);
						OnPropertyChanged(DeclaringType.GetGraphInstance(instance), originalValue, value);
					}
				}
			}

			#endregion

			#region DynamicReferenceProperty

			[Serializable]
			class DynamicReferenceProperty : GraphReferenceProperty
			{
				internal DynamicReferenceProperty(DynamicGraphType declaringType, TPropertySource property, string name, GraphType propertyType, bool isList, bool isReadOnly, Attribute[] attributes)
					: base(declaringType, name, false, propertyType, isList, isReadOnly, attributes)
				{
					this.PropertySource = property;
				}

				protected internal TPropertySource PropertySource { get; private set; }

				public new DynamicGraphType DeclaringType { get { return (DynamicGraphType)base.DeclaringType; } }

				protected internal override object GetValue(object instance)
				{
					DeclaringType.GetGraphInstance(instance).OnPropertyGet(this);
					return DeclaringType.Provider.GetPropertyValue(instance, PropertySource);
				}

				protected internal override void SetValue(object instance, object value)
				{
					object originalValue = DeclaringType.Provider.GetPropertyValue(instance, PropertySource);

					if ((originalValue == null ^ value == null) || (originalValue != null && !originalValue.Equals(value)))
					{
						DeclaringType.Provider.SetPropertyValue(instance, PropertySource, value);
						OnPropertyChanged(DeclaringType.GetGraphInstance(instance), originalValue, value);
					}
				}
			}

			#endregion
		}

		#endregion
	}

	#endregion

	#region DynamicGraphTypeProvider<TBaseType, TTypeSource, TPropertySource>

	/// <summary>
	/// Base class for type providers that expose properties dynamically but leverage base type providers
	/// for core functionality.
	/// </summary>
	/// <typeparam name="TBaseType"></typeparam>
	/// <typeparam name="TTypeSource"></typeparam>
	/// <typeparam name="TPropertySource"></typeparam>
	public abstract class DynamicGraphTypeProvider<TBaseType, TTypeSource, TPropertySource> : DynamicGraphTypeProvider<TTypeSource, TPropertySource>
        where TTypeSource : class
	{
		protected DynamicGraphTypeProvider(string @namespace)
			: base(@namespace, null)
		{ }

		internal override GraphType GetBaseType()
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
            if (!(instance is TBaseType))
                return null;

			return GetTypeSource((TBaseType)instance);
		}
	}

	#endregion
}
