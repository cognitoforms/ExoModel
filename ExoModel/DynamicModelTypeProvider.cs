using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ExoModel
{
	#region DynamicModelTypeProvider

	/// <summary>
	/// Base class for type providers that expose properties dynamically but leverage base type providers
	/// for core functionality.
	/// </summary>
	/// <typeparam name="TTypeSource"></typeparam>
	/// <typeparam name="TPropertySource"></typeparam>
	public abstract class DynamicModelTypeProvider : IModelTypeProvider
	{
		#region Fields

		string @namespace;
		string baseType;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="DynamicModelTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="@namespace"></param>
		/// <param name="baseType"></param>
		internal DynamicModelTypeProvider(string @namespace, string baseType)
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
		/// Gets the unique name of the <see cref="ModelType"/> for the specified model object instance.
		/// </summary>
		/// <param name="instance">The actual model object instance</param>
		/// <returns>The unique name of the model type for the instance if it is a valid model type, otherwise null</returns>
		protected abstract string GetModelTypeName(object instance);

		/// <summary>
		/// Creates a <see cref="ModelType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		protected abstract ModelType CreateModelType(string typeName);

		#endregion

		#region IModelTypeProvider

		/// <summary>
		/// Gets the unique name of the <see cref="ModelType"/> for the specified model object instance.
		/// </summary>
		/// <param name="instance">The actual model object instance</param>
		/// <returns>The unique name of the model type for the instance if it is a valid model type, otherwise null</returns>
		string IModelTypeProvider.GetModelTypeName(object instance)
		{
			return GetModelTypeName(instance);
		}

		/// <summary>
		/// Gets the <see cref="ModelType"/> that corresponds to the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		string IModelTypeProvider.GetModelTypeName(Type type)
		{
			// Return null to indicate that the current type provider does not support concrete types
			return null;
		}

		/// <summary>
		/// Creates a <see cref="ModelType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		ModelType IModelTypeProvider.CreateModelType(string typeName)
		{
			return CreateModelType(typeName);
		}

		#endregion
	}

	#endregion

	#region DynamicModelTypeProvider<TTypeSource, TPropertySource>

	/// <summary>
	/// Base class for type providers that expose properties dynamically but leverage base type providers
	/// for core functionality.
	/// </summary>
	/// <typeparam name="TTypeSource"></typeparam>
	/// <typeparam name="TPropertySource"></typeparam>
	public abstract class DynamicModelTypeProvider<TTypeSource, TPropertySource> : DynamicModelTypeProvider
        where TTypeSource : class
	{
		#region Constructors

		/// <summary>
		/// Creates a new <see cref="DynamicModelTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="@namespace"></param>
		/// <param name="baseType"></param>
		public DynamicModelTypeProvider(string @namespace, string baseType)
			: base(@namespace, baseType)
		{
		}

		#endregion

		#region Methods

		/// <summary>
		/// Gets the unique name of the <see cref="ModelType"/> for the specified model object instance.
		/// </summary>
		/// <param name="instance">The actual model object instance</param>
		/// <returns>The unique name of the model type for the instance if it is a valid model type, otherwise null</returns>
		protected override string GetModelTypeName(object instance)
		{
			TTypeSource type = GetTypeSource(instance);
			return type == null ? null : GetTypeName(type);
		}

		/// <summary>
		/// Creates a <see cref="ModelType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		protected override ModelType CreateModelType(string typeName)
		{
			// Exit immediately if the requested type is from a different namespace.
			if (!typeName.StartsWith(Namespace))
				return null;

			// See if a type source is available for the specified type name
			TTypeSource type = GetTypeSource(typeName);
			if (type == null)
				return null;

			// Return a new dynamic model type
			return new DynamicModelType(typeName, GetBaseType(), GetFormat(type), GetTypeAttributes(type), GetProperties(type));
		}

		internal virtual ModelType GetBaseType()
		{
			return ModelContext.Current.GetModelType(BaseType);
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

		protected virtual string GetLabel(TPropertySource property)
		{
			return null;
		}

		protected virtual string GetFormat(TPropertySource property)
		{
			return null;
		}

		protected virtual string GetHelpText(TPropertySource property)
		{
			return null;
		}

		protected virtual string GetFormat(TTypeSource type)
		{
			return null;
		}

		protected abstract bool IsList(TPropertySource property);

		protected abstract bool IsStatic(TPropertySource property);

		protected abstract bool IsReadOnly(TPropertySource property);

		protected abstract bool IsPersisted(TPropertySource property);

		protected abstract ModelType GetReferenceType(TPropertySource property);

		protected abstract TypeConverter GetValueConverter(TPropertySource property);

		protected abstract Type GetValueType(TPropertySource property);

		protected abstract object GetPropertyValue(object instance, TPropertySource property);

		protected abstract void SetPropertyValue(object instance, TPropertySource property, object value);

		protected virtual void OnCreateModelType(ModelType type)
		{ }

		#endregion

		#region DynamicModelType

		[Serializable]
		class DynamicModelType : ModelType
		{
			IEnumerable<TPropertySource> properties;

			internal DynamicModelType(string name, ModelType baseType, string format, Attribute[] attributes, IEnumerable<TPropertySource> properties)
				: base(name, baseType.QualifiedName + "+" + name, baseType, baseType.Scope, format, attributes)
			{
				this.properties = properties;
			}

			protected internal new DynamicModelTypeProvider<TTypeSource, TPropertySource> Provider
			{
				get
				{
					return base.Provider as DynamicModelTypeProvider<TTypeSource, TPropertySource>;
				}
			}

			protected internal override void OnInit()
			{
				// Get the type provider
				var provider = (DynamicModelTypeProvider<TTypeSource, TPropertySource>)Provider;

				// Automatically inherit all base type properties
				foreach (var property in BaseType.Properties)
					AddProperty(property);

				// Create model properties for each source property
				foreach (TPropertySource property in properties)
				{
					// Get the name of the property
					var name = provider.GetPropertyName(property);

					// Skip this property if it has already been added
					if (Properties.Contains(name))
						continue;

					// Get the label for the property
					var label = provider.GetLabel(property);

					// Get the help text for the property
					var helptext = provider.GetHelpText(property);

					// Get the format for the property
					var format = provider.GetFormat(property);

					// Determine if the property is a list
					var isList = provider.IsList(property);

					// Determine if the property is a static property.
					var isStatic = provider.IsStatic(property);

					// Determine if the property is read only
					var isReadOnly = provider.IsReadOnly(property);

					// Determine if the property is persisted
					var isPersisted = provider.IsPersisted(property);

					// Get the attributes for the property
					var attributes = provider.GetPropertyAttributes(property);

					// Determine whether the property is a reference or value type
					var referenceType = provider.GetReferenceType(property);

					// Add the new value or reference property
					if (referenceType == null)
						AddProperty(
							new DynamicValueProperty(
								this, property, name, label, helptext, format, isStatic, provider.GetValueType(property), provider.GetValueConverter(property), isList, isReadOnly, isPersisted, attributes)
						);
					else
						AddProperty(
							new DynamicReferenceProperty(
								this, property, name, label, helptext, format, isStatic, referenceType, isList, isReadOnly, isPersisted, attributes)
						);

				}

				// Remove the reference to the property source to avoid caching instance data with the type
				this.properties = null;

				// Notify provider subclasses that the model type has been created
				provider.OnCreateModelType(this);
			}

			protected internal override IList ConvertToList(ModelReferenceProperty property, object list)
			{
				return BaseType.ConvertToList(property, list);
			}

			protected internal override void SaveInstance(ModelInstance modelInstance)
			{
				BaseType.SaveInstance(modelInstance);
			}

			public override ModelInstance GetModelInstance(object instance)
			{
				return BaseType.GetModelInstance(instance);
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

			protected internal override bool GetIsDeleted(object instance)
			{
				return BaseType.GetIsDeleted(instance);
			}

			protected internal override bool GetIsModified(object instance)
			{
				return BaseType.GetIsModified(instance);
			}

			protected internal override bool GetIsPendingDelete(object instance)
			{
				return BaseType.GetIsPendingDelete(instance);
			}

			protected internal override void SetIsPendingDelete(object instance, bool isPendingDelete)
			{
				BaseType.SetIsPendingDelete(instance, isPendingDelete);
			}

			protected internal override void OnStartTrackingList(ModelInstance instance, ModelReferenceProperty property, IList list)
			{
				Provider.GetBaseType().OnStartTrackingList(instance, property, list);
			}

			protected internal override void OnStopTrackingList(ModelInstance instance, ModelReferenceProperty property, IList list)
			{
				Provider.GetBaseType().OnStopTrackingList(instance, property, list);
			}

			#region DescriptorValueProperty

			[Serializable]
			class DynamicValueProperty : ModelValueProperty
			{
				internal DynamicValueProperty(DynamicModelType declaringType, TPropertySource property, string name, string label, string helptext, string format, bool isStatic, Type propertyType, TypeConverter converter, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
					: base(declaringType, name, label, helptext, format, isStatic, propertyType, converter, isList, isReadOnly, isPersisted, attributes)
				{
					this.PropertySource = property;
				}

				protected internal TPropertySource PropertySource { get; private set; }

				public new DynamicModelType DeclaringType { get { return (DynamicModelType)base.DeclaringType; } }

				protected internal override object GetValue(object instance)
				{
					DeclaringType.GetModelInstance(instance).OnPropertyGet(this);
					return DeclaringType.Provider.GetPropertyValue(instance, PropertySource);
				}

				protected internal override void SetValue(object instance, object value)
				{
					object originalValue = DeclaringType.Provider.GetPropertyValue(instance, PropertySource);

					if ((originalValue == null ^ value == null) || (originalValue != null && !originalValue.Equals(value)))
					{
						DeclaringType.Provider.SetPropertyValue(instance, PropertySource, value);
						OnPropertyChanged(DeclaringType.GetModelInstance(instance), originalValue, value);
					}
				}
			}

			#endregion

			#region DynamicReferenceProperty

			[Serializable]
			class DynamicReferenceProperty : ModelReferenceProperty
			{
				internal DynamicReferenceProperty(DynamicModelType declaringType, TPropertySource property, string name, string label, string helptext, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
					: base(declaringType, name, label, helptext, format, isStatic, propertyType, isList, isReadOnly, isPersisted, attributes)
				{
					this.PropertySource = property;
				}

				protected internal TPropertySource PropertySource { get; private set; }

				public new DynamicModelType DeclaringType { get { return (DynamicModelType)base.DeclaringType; } }

				protected internal override object GetValue(object instance)
				{
					if (!IsStatic)
						DeclaringType.GetModelInstance(instance).OnPropertyGet(this);

					return DeclaringType.Provider.GetPropertyValue(instance, PropertySource);
				}

				protected internal override void SetValue(object instance, object value)
				{
					object originalValue = DeclaringType.Provider.GetPropertyValue(instance, PropertySource);

					if (!IsStatic && (originalValue == null ^ value == null) || (originalValue != null && !originalValue.Equals(value)))
					{
						DeclaringType.Provider.SetPropertyValue(instance, PropertySource, value);
						OnPropertyChanged(DeclaringType.GetModelInstance(instance), originalValue, value);
					}
				}
			}

			#endregion
		}

		#endregion
	}

	#endregion

	#region DynamicModelTypeProvider<TBaseType, TTypeSource, TPropertySource>

	/// <summary>
	/// Base class for type providers that expose properties dynamically but leverage base type providers
	/// for core functionality.
	/// </summary>
	/// <typeparam name="TBaseType"></typeparam>
	/// <typeparam name="TTypeSource"></typeparam>
	/// <typeparam name="TPropertySource"></typeparam>
	public abstract class DynamicModelTypeProvider<TBaseType, TTypeSource, TPropertySource> : DynamicModelTypeProvider<TTypeSource, TPropertySource>
        where TTypeSource : class
	{
		protected DynamicModelTypeProvider(string @namespace)
			: base(@namespace, null)
		{ }

		internal override ModelType GetBaseType()
		{
			return ModelContext.Current.GetModelType<TBaseType>();
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
