using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Linq.Expressions;

namespace ExoModel
{
	/// <summary>
	/// Base class for model contexts that work with strongly-typed object models based on compiled types
	/// using inheritence and declared properties for associations and intrinsic types.
	/// </summary>
	public abstract class ReflectionModelTypeProvider : IModelTypeProvider
	{
		#region Fields

		Dictionary<string, Type> typeNames = new Dictionary<string, Type>();
		HashSet<Type> supportedTypes = new HashSet<Type>();
		HashSet<Type> declaringTypes = new HashSet<Type>();
		string @namespace;
		bool isCacheable;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="ReflectionModelTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create model types from</param>
		public ReflectionModelTypeProvider(params Assembly[] assemblies)
			: this("", assemblies)
		{ }

		/// <summary>
		/// Creates a new <see cref="ReflectionModelTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create model types from</param>
		public ReflectionModelTypeProvider(string @namespace, params Assembly[] assemblies)
			: this(@namespace,
				assemblies
				.SelectMany(a => a.GetTypes())
				.Where(t => typeof(IModelInstance).IsAssignableFrom(t)), null, true)
		{ }

		/// <summary>
		/// Creates a new <see cref="ReflectionModelTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create model types from</param>
		public ReflectionModelTypeProvider(IEnumerable<Type> types)
			: this("", types, null, true)
		{ }

		/// <summary>
		/// Creates a new <see cref="ReflectionModelTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create model types from</param>
		public ReflectionModelTypeProvider(string @namespace, IEnumerable<Type> types)
			: this(@namespace, types, null, true)
		{ }

		/// <summary>
		/// Creates a new <see cref="ReflectionModelTypeProvider"/> based on the specified types
		/// and also including properties declared on the specified base types.
		/// </summary>
		/// <param name="types">The types to create model types from</param>
		/// <param name="baseTypes">The base types that contain properties to include on model types</param>
		public ReflectionModelTypeProvider(string @namespace, IEnumerable<Type> types, IEnumerable<Type> baseTypes, bool isCacheable)
		{
			// The list of types cannot be null
			if (types == null)
				throw new ArgumentNullException("types");

			this.@namespace = string.IsNullOrEmpty(@namespace) ? string.Empty : @namespace + ".";
			this.isCacheable = isCacheable;

			// The list of base types is not required, so convert null to empty set
			if (baseTypes == null)
				baseTypes = new Type[0];

			// Create dictionaries of type names and valid supported and declaring types to introspect
			foreach (Type type in types)
			{
				typeNames.Add(GetNamespace(this.@namespace, type) + type.Name, type);
				if (!supportedTypes.Contains(type))
					supportedTypes.Add(type);
				if (!declaringTypes.Contains(type))
					declaringTypes.Add(type);
			}
			foreach (Type baseType in baseTypes)
			{
				if (!declaringTypes.Contains(baseType))
					declaringTypes.Add(baseType);
			}
		}

		#endregion

		#region Properties

		public string[] DefaultFormatProperties { get; set; }

		#endregion

		#region Methods

		/// <summary>
		/// Adds a property to the specified <see cref="ModelType"/> that represents an
		/// association with another <see cref="ModelType"/> instance.
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="isStatic">Indicates whether the property is statically defined on the type</param>
		/// <param name="propertyType">The <see cref="ModelType"/> of the property</param>
		/// <param name="isList">Indicates whether the property represents a list of references or a single reference</param>
		/// <param name="attributes">The attributes assigned to the property</param>
		protected virtual ModelReferenceProperty CreateReferenceProperty(ModelType declaringType, PropertyInfo property, string name, string label, string helptext, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
		{
			return new ReflectionReferenceProperty(declaringType, property, name, label, helptext, format, isStatic, propertyType, isList, isReadOnly, isPersisted, attributes);
		}

		/// <summary>
		/// Adds a property to the specified <see cref="ModelType"/> that represents an
		/// strongly-typed value value with the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="propertyType">The <see cref="Type"/> of the property</param>
		/// <param name="converter">The optional value type converter to use</param>
		/// <param name="attributes">The attributes assigned to the property</param>
		protected virtual ModelValueProperty CreateValueProperty(ModelType declaringType, PropertyInfo property, string name, string label, string helptext, string format, bool isStatic, Type propertyType, TypeConverter converter, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
		{
			return new ReflectionValueProperty(declaringType, property, name, label, helptext, format, isStatic, propertyType, converter, isList, isReadOnly, isPersisted, attributes);
		}

		protected virtual ModelMethod CreateMethod(ModelType declaringType, MethodInfo method, string name, bool isStatic, Attribute[] attributes)
		{
			return new ReflectionModelMethod(declaringType, method, name, isStatic, attributes);
		}

		protected virtual Type GetUnderlyingType(object instance)
		{
			return instance.GetType();
		}

		protected virtual string GetNamespace(string @namespace, Type type)
		{
			return @namespace;
		}

		#endregion

		#region IModelTypeProvider

		bool IModelTypeProvider.IsCachable { get { return isCacheable; } }

		string IModelTypeProvider.Namespace { get { return @namespace; } }

		/// <summary>
		/// Gets the unique name of the <see cref="ModelType"/> for the specified model object instance.
		/// </summary>
		/// <param name="instance">The actual model object instance</param>
		/// <returns>The unique name of the model type for the instance if it is a valid model type, otherwise null</returns>
		string IModelTypeProvider.GetModelTypeName(object instance)
		{
			Type underlyingType = GetUnderlyingType(instance);
			return supportedTypes.Contains(underlyingType) ? GetNamespace(this.@namespace, underlyingType) + underlyingType.Name : null;
		}

		/// <summary>
		/// Gets the <see cref="ModelType"/> that corresponds to the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		string IModelTypeProvider.GetModelTypeName(Type type)
		{
			return supportedTypes.Contains(type) ? GetNamespace(this.@namespace, type) + type.Name : null;
		}

		/// <summary>
		/// Creates a <see cref="ModelType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		ModelType IModelTypeProvider.CreateModelType(string typeName)
		{
			Type type;

			// Get the type that corresponds to the specified type name
			if (!typeNames.TryGetValue(typeName, out type))
				return null;

			// Get the default reference format for the type
			string format = type.GetCustomAttributes(true).OfType<ModelFormatAttribute>().Select(a => a.Format).FirstOrDefault();

			// If a format was not found, see if the type has a property that is in the set of default format properties
			if (format == null && DefaultFormatProperties != null)
				format = DefaultFormatProperties.Where(p => type.GetProperty(p) != null).Select(p => "[" + p + "]").FirstOrDefault();

			// Create the new model type
			return CreateModelType(GetNamespace(this.@namespace, type), type, format);
		}

		/// <summary>
		/// Allows subclasses to create specific subclasses of <see cref="ReflectionModelType"/>.
		/// </summary>
		/// <param name="namespace"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		protected abstract ReflectionModelType CreateModelType(string @namespace, Type type, string format);

		#endregion

		#region ReflectionModelType

		/// <summary>
		/// Concrete subclass of <see cref="ModelType"/> that represents a specific <see cref="Type"/>.
		/// </summary>
		[Serializable]
		public abstract class ReflectionModelType : ModelType, IReflectionModelType
		{
			protected internal ReflectionModelType(string @namespace, Type type, string scope, string format)
				: base(@namespace + type.Name, type.AssemblyQualifiedName, GetBaseType(type), scope, format, type.GetCustomAttributes(false).Cast<Attribute>().ToArray())
			{
				this.UnderlyingType = type;
			}

			static ModelType GetBaseType(Type type)
			{
				ModelContext context = ModelContext.Current;
				ModelType baseModelType = null;
				for (Type baseType = type.BaseType; baseModelType == null && baseType != null; baseType = baseType.BaseType)
					baseModelType = context.GetModelType(baseType);
				return baseModelType;
			}

			public Type UnderlyingType { get; private set; }

			public override ModelInstance GetModelInstance(object instance)
			{
				if (instance is IModelInstance)
					return ((IModelInstance)instance).Instance;

				return null;
			}

			protected internal override void OnInit()
			{
				ReflectionModelTypeProvider provider = (ReflectionModelTypeProvider)Provider;

				Type listItemType;

				// Determine if any inherited properties on the type have been replaced using the new keyword
				Dictionary<string, bool> isNewProperty = new Dictionary<string, bool>();
				foreach (PropertyInfo property in UnderlyingType.GetProperties())
					isNewProperty[property.Name] = isNewProperty.ContainsKey(property.Name);

				// Process all properties on the instance type to create references.  Process static properties
				// last since they would otherwise complicate calculated indexes when dealing with sub types.
				foreach (PropertyInfo property in GetEligibleProperties().OrderBy(p => (p.GetGetMethod(true) ?? p.GetSetMethod(true)).IsStatic).ThenBy(p => p.Name))
				{
					// Exit immediately if the property was not in the list of valid declaring types
					if (ModelContext.Current.GetModelType(property.DeclaringType) == null || (isNewProperty[property.Name] && property.DeclaringType != UnderlyingType))
						continue;

					ModelType referenceType;

					// Copy properties inherited from base model types
					if (BaseType != null && BaseType.Properties.Contains(property.Name) && !(isNewProperty[property.Name] || property.GetGetMethod().IsStatic))
						AddProperty(BaseType.Properties[property.Name]);

					// Create references based on properties that relate to other instance types
					else if ((referenceType = Context.GetModelType(property.PropertyType)) != null)
					{
						var format = property.GetCustomAttributes(true).OfType<ModelFormatAttribute>().Select(a => a.Format).FirstOrDefault();
						ModelReferenceProperty reference = provider.CreateReferenceProperty(this, property, property.Name, null, null, format, property.GetGetMethod().IsStatic, referenceType, false, property.GetSetMethod() == null, property.GetSetMethod() != null, property.GetCustomAttributes(true).Cast<Attribute>().ToArray());
						if (reference != null)
							AddProperty(reference);
					}

					// Create references based on properties that are lists of other instance types
					else if (TryGetListItemType(property.PropertyType, out listItemType) && (referenceType = Context.GetModelType(listItemType)) != null)
					{
						var format = property.GetCustomAttributes(true).OfType<ModelFormatAttribute>().Select(a => a.Format).FirstOrDefault();
						ModelReferenceProperty reference = provider.CreateReferenceProperty(this, property, property.Name, null, null, format, property.GetGetMethod().IsStatic, referenceType, true, false, true, property.GetCustomAttributes(true).Cast<Attribute>().ToArray());
						if (reference != null)
							AddProperty(reference);
					}

					// Create values for all other properties
					else
					{
						var value = provider.CreateValueProperty(this, property, property.Name, null, null, null, property.GetGetMethod().IsStatic, property.PropertyType, TypeDescriptor.GetConverter(property.PropertyType), TryGetListItemType(property.PropertyType, out listItemType), property.GetSetMethod() == null, property.GetSetMethod() != null, property.GetCustomAttributes(true).Cast<Attribute>().ToArray());

						if (value != null)
							AddProperty(value);
					}
				}

				// Process all public methods on the underlying type
				foreach (MethodInfo method in UnderlyingType.GetMethods()
					.Where(method => !method.IsSpecialName && method.Name != "ToString" && (method.DeclaringType == UnderlyingType || (BaseType != null && BaseType.Methods.Contains(method.Name)))))
				{
					ModelMethod gm = provider.CreateMethod(this, method, method.Name, method.IsStatic, method.GetCustomAttributes(true).Cast<Attribute>().ToArray());
					if (gm != null)
						AddMethod(gm);
				}

				// Force all subtypes to load too
				foreach (Type subType in provider.supportedTypes.Where(t => t.IsSubclassOf(UnderlyingType)))
					Context.GetModelType(subType);
			}

			/// <summary>
			/// Gets the set of eligible properties that should be considered valid model properties.
			/// </summary>
			/// <returns></returns>
			protected virtual IEnumerable<PropertyInfo> GetEligibleProperties()
			{
				return UnderlyingType.GetProperties();
			}

			/// <summary>
			/// Converts the specified object into a instance that implements <see cref="IList"/>.
			/// </summary>
			/// <param name="list"></param>
			/// <returns></returns>
			protected internal override IList ConvertToList(ModelReferenceProperty property, object list)
			{
				if (list == null)
					return null;

				if (list is IList)
					return (IList)list;

				if (list is IListSource)
					return ((IListSource)list).GetList();

				if (property is ReflectionReferenceProperty)
					return null;

				// Add ICollection<T> support

				throw new NotSupportedException("Unable to convert the specified list instance into a valid IList implementation.");
			}

			protected internal override void OnStartTrackingList(ModelInstance instance, ModelReferenceProperty property, IList list)
			{
				if (list is INotifyCollectionChanged)
					(new ListChangeEventAdapter(instance, property, (INotifyCollectionChanged)list)).Start();
				else
					base.OnStartTrackingList(instance, property, list);
			}

			protected internal override void OnStopTrackingList(ModelInstance instance, ModelReferenceProperty property, IList list)
			{
				if (list is INotifyCollectionChanged)
					(new ListChangeEventAdapter(instance, property, (INotifyCollectionChanged)list)).Stop();
				else
					base.OnStartTrackingList(instance, property, list);
			}

			#region ListChangeEventAdapter

			[Serializable]
			class ListChangeEventAdapter
			{
				ModelInstance instance;
				ModelReferenceProperty property;
				INotifyCollectionChanged list;

				public ListChangeEventAdapter(ModelInstance instance, ModelReferenceProperty property, INotifyCollectionChanged list)
				{
					this.instance = instance;
					this.property = property;
					this.list = list;
				}

				public void Start()
				{
					list.CollectionChanged += this.list_CollectionChanged;
				}

				public void Stop()
				{
					list.CollectionChanged -= this.list_CollectionChanged;
				}

				void list_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
				{
					((ReflectionModelType)instance.Type).OnListChanged(instance, property,
						e.Action == NotifyCollectionChangedAction.Add ? e.NewItems : new object[0],
						e.Action == NotifyCollectionChangedAction.Remove ? e.OldItems : new object[0]);
				}

				public override bool Equals(object obj)
				{
					var that = obj as ListChangeEventAdapter;

					if (that == null)
						return false;

					return that.instance == this.instance && that.property == this.property && that.list == this.list;
				}

				public override int GetHashCode()
				{
					return instance.GetHashCode();
				}
			}

			#endregion
		}

		#endregion

		#region ReflectionValueProperty

		[Serializable]
		public class ReflectionValueProperty : ModelValueProperty, IReflectionModelProperty
		{
			protected internal ReflectionValueProperty(ModelType declaringType, PropertyInfo property, string name, string label, string helptext, string format, bool isStatic, Type propertyType, TypeConverter converter, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes, LambdaExpression defaultValue = null)
				: base(declaringType, name, label, helptext, format, isStatic, propertyType, converter, isList, isReadOnly, isPersisted, attributes, defaultValue)
			{
				this.PropertyInfo = property;
			}

			public PropertyInfo PropertyInfo { get; private set; }

			protected internal override object GetValue(object instance)
			{
				return PropertyInfo.GetValue(instance, null);
			}

			protected internal override void SetValue(object instance, object value)
			{
				try
				{
					PropertyInfo.SetValue(instance, value, null);
				}
				catch (ArgumentException e)
				{
					if (e.Message == "Property set method not found.")
						throw new ArgumentException(string.Format("Property set method not found on property {0} of type {1}.", PropertyInfo.Name, PropertyInfo.DeclaringType.Name));

					throw e;
				}
			}

			public Type UnderlyingType
			{
				get { return PropertyInfo.PropertyType; }
			}
		}

		#endregion

		#region ReflectionReferenceProperty

		[Serializable]
		public class ReflectionReferenceProperty : ModelReferenceProperty, IReflectionModelProperty
		{
			protected internal ReflectionReferenceProperty(ModelType declaringType, PropertyInfo property, string name, string label, string helptext, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
				: base(declaringType, name, label, helptext, format, isStatic, propertyType, isList, isReadOnly, isPersisted, attributes)
			{
				this.PropertyInfo = property;
			}

			public PropertyInfo PropertyInfo { get; private set; }

			protected internal override object GetValue(object instance)
			{
				return PropertyInfo.GetValue(instance, null);
			}

			protected internal override void SetValue(object instance, object value)
			{
				PropertyInfo.SetValue(instance, value, null);
			}

			public Type UnderlyingType
			{
				get { return PropertyInfo.PropertyType; }
			}
		}

		#endregion

		#region ReflectionModelMethod

		protected class ReflectionModelMethod : ModelMethod
		{
			MethodInfo method;

			protected internal ReflectionModelMethod(ModelType declaringType, MethodInfo method, string name, bool isStatic, Attribute[] attributes)
				: base(declaringType, name, isStatic, attributes)
			{
				this.method = method;

				foreach (var parameter in method.GetParameters())
				{
					ModelType referenceType = ModelContext.Current.GetModelType(parameter.ParameterType);
					Type listItemType;
					bool isList = false;

					if (referenceType == null && ((ReflectionModelType)declaringType).TryGetListItemType(parameter.ParameterType, out listItemType))
					{
						referenceType = ModelContext.Current.GetModelType(listItemType);
						isList = referenceType != null;
					}
					AddParameter(new ReflectionModelMethodParameter(this, parameter.Name, parameter.ParameterType, referenceType, isList));
				}
			}

			public override object Invoke(ModelInstance instance, params object[] args)
			{
				return method.Invoke(method.IsStatic ? null : instance.Instance, args);
			}
		}

		#endregion

		#region ReflectionModelMethodParameter

		protected class ReflectionModelMethodParameter : ModelMethodParameter
		{
			#region Constructors

			protected internal ReflectionModelMethodParameter(ModelMethod method, string name, Type parameterType, ModelType referenceType, bool isList)
				: base(method, name, parameterType, referenceType, isList)
			{ }

			#endregion
		}

		#endregion

		#region TypeComparer

		/// <summary>
		/// Specialized <see cref="IComparer<T>"/> implementation that sorts types
		/// first in order of inheritance and second by name.
		/// </summary>
		class TypeComparer : Comparer<Type>
		{
			public override int Compare(Type x, Type y)
			{
				if (x == y)
					return 0;
				else if (x.IsSubclassOf(y))
					return 1;
				else if (y.IsSubclassOf(x))
					return -1;
				else if (x.BaseType == y.BaseType)
					return x.FullName.CompareTo(y.FullName);
				else
					return GetQualifiedTypeName(x).CompareTo(GetQualifiedTypeName(y));
			}

			/// <summary>
			/// Gets the fully-qualified name of the type including all base classes.
			/// </summary>
			/// <param name="type"></param>
			/// <returns></returns>
			string GetQualifiedTypeName(Type type)
			{
				string typeName = "";
				while (type != null)
				{
					typeName = type.FullName + ":" + typeName;
					type = type.BaseType;
				}
				return typeName;
			}
		}

		#endregion
	}
}
