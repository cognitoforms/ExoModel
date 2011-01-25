using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using System.Collections.Specialized;

namespace ExoGraph
{
	/// <summary>
	/// Base class for graph contexts that work with strongly-typed object graphs based on compiled types
	/// using inheritence and declared properties for associations and intrinsic types.
	/// </summary>
	public abstract class ReflectionGraphTypeProvider : IGraphTypeProvider
	{
		#region Fields

		Dictionary<string, Type> typeNames = new Dictionary<string, Type>();
		HashSet<Type> supportedTypes = new HashSet<Type>();
		HashSet<Type> declaringTypes = new HashSet<Type>();
		string @namespace;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="ReflectionGraphTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create graph types from</param>
		public ReflectionGraphTypeProvider(IEnumerable<Type> types)
			: this("", types, null)
		{ }

		/// <summary>
		/// Creates a new <see cref="ReflectionGraphTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create graph types from</param>
		public ReflectionGraphTypeProvider(string @namespace, IEnumerable<Type> types)
			: this(@namespace, types, null)
		{ }

		/// <summary>
		/// Creates a new <see cref="ReflectionGraphTypeProvider"/> based on the specified types
		/// and also including properties declared on the specified base types.
		/// </summary>
		/// <param name="types">The types to create graph types from</param>
		/// <param name="baseTypes">The base types that contain properties to include on graph types</param>
		public ReflectionGraphTypeProvider(string @namespace, IEnumerable<Type> types, IEnumerable<Type> baseTypes)
		{
			// The list of types cannot be null
			if (types == null)
				throw new ArgumentNullException("types");

			this.@namespace = string.IsNullOrEmpty(@namespace) ? string.Empty : @namespace + ".";

			// The list of base types is not required, so convert null to empty set
			if (baseTypes == null)
				baseTypes = new Type[0];

			// Create dictionaries of type names and valid supported and declaring types to introspect
			foreach (Type type in types)
			{
				typeNames.Add(this.@namespace + type.Name, type);
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

		#endregion

		#region Methods

		/// <summary>
		/// Adds a property to the specified <see cref="GraphType"/> that represents an
		/// association with another <see cref="GraphType"/> instance.
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="isStatic">Indicates whether the property is statically defined on the type</param>
		/// <param name="propertyType">The <see cref="GraphType"/> of the property</param>
		/// <param name="isList">Indicates whether the property represents a list of references or a single reference</param>
		/// <param name="attributes">The attributes assigned to the property</param>
		protected virtual GraphReferenceProperty CreateReferenceProperty(GraphType declaringType, PropertyInfo property, string name, bool isStatic, GraphType propertyType, bool isList, Attribute[] attributes)
		{
			return new ReflectionReferenceProperty(declaringType, property, name, isStatic, propertyType, isList, attributes);
		}

		/// <summary>
		/// Adds a property to the specified <see cref="GraphType"/> that represents an
		/// strongly-typed value value with the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="propertyType">The <see cref="Type"/> of the property</param>
		/// <param name="converter">The optional value type converter to use</param>
		/// <param name="attributes">The attributes assigned to the property</param>
		protected virtual GraphValueProperty CreateValueProperty(GraphType declaringType, PropertyInfo property, string name, bool isStatic, Type propertyType, TypeConverter converter, bool isList, Attribute[] attributes)
		{
			return new ReflectionValueProperty(declaringType, property, name, isStatic, propertyType, converter, isList, attributes);
		}

		protected virtual GraphMethod CreateMethod(GraphType declaringType, MethodInfo method, string name, bool isStatic, Attribute[] attributes)
		{
			return new ReflectionGraphMethod(declaringType, method, name, isStatic, attributes);
		}

		protected virtual Type GetUnderlyingType(object instance)
		{
			return instance.GetType();
		}

		/// <summary>
		/// Gets the fully qualified name of the scope that the current instance is in
		/// </summary>
		protected abstract string GetScopeName(GraphInstance instance);

		protected static GraphInstance CreateGraphInstance(object instance)
		{
			return new GraphInstance(instance);
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
			return supportedTypes.Contains(GetUnderlyingType(instance)) ? @namespace + GetUnderlyingType(instance).Name : null;
		}

		/// <summary>
		/// Gets the <see cref="GraphType"/> that corresponds to the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		string IGraphTypeProvider.GetGraphTypeName(Type type)
		{
			return supportedTypes.Contains(type) ? @namespace + type.Name : null;
		}

		/// <summary>
		/// Gets the fully qualified name of the scope that the current instance is in
		/// </summary>
		string IGraphTypeProvider.GetScopeName(GraphInstance instance)
		{
			return GetScopeName(instance);
		}

		/// <summary>
		/// Creates a <see cref="GraphType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		GraphType IGraphTypeProvider.CreateGraphType(string typeName)
		{
			Type type;

			// Get the type that corresponds to the specified type name
			if (!typeNames.TryGetValue(typeName, out type))
				return null;

			// Create the new graph type
			return CreateGraphType(@namespace, type);
		}

		/// <summary>
		/// Allows subclasses to create specific subclasses of <see cref="ReflectionGraphType"/>.
		/// </summary>
		/// <param name="namespace"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		protected abstract ReflectionGraphType CreateGraphType(string @namespace, Type type);

		#endregion

		#region ReflectionGraphType

		/// <summary>
		/// Concrete subclass of <see cref="GraphType"/> that represents a specific <see cref="Type"/>.
		/// </summary>
		[Serializable]
		public abstract class ReflectionGraphType : GraphType
		{
			protected internal ReflectionGraphType(string @namespace, Type type)
				: base(@namespace + type.Name, type.AssemblyQualifiedName, GetBaseType(type), type.GetCustomAttributes(false).Cast<Attribute>().ToArray())
			{
				this.UnderlyingType = type;
			}

			static GraphType GetBaseType(Type type)
			{
				GraphContext context = GraphContext.Current;
				GraphType baseGraphType = null;
				for (Type baseType = type.BaseType; baseGraphType == null && baseType != null; baseType = baseType.BaseType)
					baseGraphType = context.GetGraphType(baseType);
				return baseGraphType;
			}

			public Type UnderlyingType { get; private set; }

			protected internal override void OnInit()
			{
				ReflectionGraphTypeProvider provider = (ReflectionGraphTypeProvider)Provider;

				Type listItemType;

				// Determine if any inherited properties on the type have been replaced using the new keyword
				Dictionary<string, bool> isNewProperty = new Dictionary<string, bool>();
				foreach (PropertyInfo property in UnderlyingType.GetProperties())
					isNewProperty[property.Name] = isNewProperty.ContainsKey(property.Name);

				// Process all properties on the instance type to create references.  Process static properties
				// last since they would otherwise complicate calculated indexes when dealing with sub types.
				foreach (PropertyInfo property in GetEligibleProperties())
				{
					// Exit immediately if the property was not in the list of valid declaring types
					if (GraphContext.Current.GetGraphType(property.DeclaringType) == null || (isNewProperty[property.Name] && property.DeclaringType != UnderlyingType))
						continue;

					// Copy properties inherited from base graph types
					if (BaseType != null && BaseType.Properties.Contains(property.Name) && !(isNewProperty[property.Name] || property.GetGetMethod().IsStatic))
						AddProperty(BaseType.Properties[property.Name]);

					// Create references based on properties that relate to other instance types
					else if (provider.supportedTypes.Contains(property.PropertyType))
					{
						GraphReferenceProperty reference = provider.CreateReferenceProperty(this, property, property.Name, property.GetGetMethod().IsStatic, Context.GetGraphType(property.PropertyType), false, property.GetCustomAttributes(true).Cast<Attribute>().ToArray());
						if (reference != null)
							AddProperty(reference);
					}

					// Create references based on properties that are lists of other instance types
					else if (TryGetListItemType(property.PropertyType, out listItemType) && provider.supportedTypes.Contains(listItemType))
					{
						GraphReferenceProperty reference = provider.CreateReferenceProperty(this, property, property.Name, property.GetGetMethod().IsStatic, Context.GetGraphType(listItemType), true, property.GetCustomAttributes(true).Cast<Attribute>().ToArray());
						if (reference != null)
							AddProperty(reference);
					}

					// Create values for all other properties
					else
					{
						var value = provider.CreateValueProperty(this, property, property.Name, property.GetGetMethod().IsStatic, property.PropertyType, TypeDescriptor.GetConverter(property.PropertyType), TryGetListItemType(property.PropertyType, out listItemType), property.GetCustomAttributes(true).Cast<Attribute>().ToArray());
						
						if (value != null)
							AddProperty(value);
					}
				}

				// Process all public methods on the underlying type
				foreach (MethodInfo method in UnderlyingType.GetMethods()
					.Where(method => !method.IsSpecialName && method.Name != "ToString" && (method.DeclaringType == UnderlyingType || (BaseType != null && BaseType.Methods.Contains(method.Name)))))
				{
					GraphMethod gm = provider.CreateMethod(this, method, method.Name, method.IsStatic, method.GetCustomAttributes(true).Cast<Attribute>().ToArray());
					if (gm != null)
						AddMethod(gm);
				}
			}

			/// <summary>
			/// Gets the set of eligible properties that should be considered valid graph properties.
			/// </summary>
			/// <returns></returns>
			protected virtual IEnumerable<PropertyInfo> GetEligibleProperties()
			{
				return UnderlyingType.GetProperties().OrderBy(p => (p.GetGetMethod(true) ?? p.GetSetMethod(true)).IsStatic);
			}

			/// <summary>
			/// Converts the specified object into a instance that implements <see cref="IList"/>.
			/// </summary>
			/// <param name="list"></param>
			/// <returns></returns>
			protected internal override IList ConvertToList(GraphReferenceProperty property, object list)
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

			protected internal override void OnStartTrackingList(GraphInstance instance, GraphReferenceProperty property, IList list)
			{
				if (list is INotifyCollectionChanged)
					(new ListChangeEventAdapter(instance, property, (INotifyCollectionChanged)list)).Start();
				else
					base.OnStartTrackingList(instance, property, list);
			}

			protected internal override void OnStopTrackingList(GraphInstance instance, GraphReferenceProperty property, IList list)
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
				GraphInstance instance;
				GraphReferenceProperty property;
				INotifyCollectionChanged list;

				public ListChangeEventAdapter(GraphInstance instance, GraphReferenceProperty property, INotifyCollectionChanged list)
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
					((ReflectionGraphType)instance.Type).OnListChanged(instance, property,
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
		protected class ReflectionValueProperty : GraphValueProperty
		{
			protected internal ReflectionValueProperty(GraphType declaringType, PropertyInfo property, string name, bool isStatic, Type propertyType, TypeConverter converter, bool isList, Attribute[] attributes)
				: base(declaringType, name, isStatic, propertyType, converter, isList, attributes)
			{
				this.PropertyInfo = property;
			}

			protected internal PropertyInfo PropertyInfo { get; private set; }

			protected internal override object GetValue(object instance)
			{
				return PropertyInfo.GetValue(instance, null);
			}

			protected internal override void SetValue(object instance, object value)
			{
				PropertyInfo.SetValue(instance, value, null);
			}
		}

		#endregion

		#region ReflectionReferenceProperty

		[Serializable]
		protected class ReflectionReferenceProperty : GraphReferenceProperty
		{
			protected internal ReflectionReferenceProperty(GraphType declaringType, PropertyInfo property, string name, bool isStatic, GraphType propertyType, bool isList, Attribute[] attributes)
				: base(declaringType, name, isStatic, propertyType, isList, attributes)
			{
				this.PropertyInfo = property;
			}

			protected internal PropertyInfo PropertyInfo { get; private set; }

			protected internal override object GetValue(object instance)
			{
				return PropertyInfo.GetValue(instance, null);
			}

			protected internal override void SetValue(object instance, object value)
			{
				PropertyInfo.SetValue(instance, value, null);
			}
		}

		#endregion

		#region ReflectionGraphMethod

		protected class ReflectionGraphMethod : GraphMethod
		{
			MethodInfo method;

			protected internal ReflectionGraphMethod(GraphType declaringType, MethodInfo method, string name, bool isStatic, Attribute[] attributes)
				: base(declaringType, name, isStatic, attributes)
			{
				this.method = method;

				foreach (var parameter in method.GetParameters())
				{
					GraphType referenceType = GraphContext.Current.GetGraphType(parameter.ParameterType);
					Type listType;

					if (referenceType == null)
					{
						if (((ReflectionGraphType)declaringType).TryGetListItemType(parameter.ParameterType, out listType))
							referenceType = GraphContext.Current.GetGraphType(listType);

						// List Type
						if (referenceType == null)
							AddParameter(new ReflectionGraphMethodParameter(this, parameter.Name, parameter.ParameterType));
						
						// Reference Type
						else
							AddParameter(new ReflectionGraphMethodParameter(this, parameter.Name, referenceType, true));
					}

					// Value Type
					else
						AddParameter(new ReflectionGraphMethodParameter(this, parameter.Name, referenceType, false));
				}
			}

			public override object Invoke(GraphInstance instance, params object[] args)
			{
				return method.Invoke(method.IsStatic ? null : instance.Instance, args);
			}
		}

		#endregion

		#region ReflectionGraphMethodParameter

		protected class ReflectionGraphMethodParameter : GraphMethodParameter
		{
			#region Constructors

			protected internal ReflectionGraphMethodParameter(GraphMethod method, string name, Type valueType)
				: base(method, name, valueType)
			{ }

			protected internal ReflectionGraphMethodParameter(GraphMethod method, string name, GraphType referenceType, bool isList)
				: base(method, name, referenceType, isList)
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
