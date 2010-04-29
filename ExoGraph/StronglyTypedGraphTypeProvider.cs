﻿using System;
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
	public abstract class StronglyTypedGraphTypeProvider : IGraphTypeProvider
	{
		#region Fields

		Dictionary<string, Type> typeNames = new Dictionary<string, Type>();
		HashSet<Type> supportedTypes = new HashSet<Type>();
		HashSet<Type> declaringTypes = new HashSet<Type>();
		Func<GraphInstance, object> extensionFactory;
		string @namespace;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="StronglyTypesGraphContext"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create graph types from</param>
		public StronglyTypedGraphTypeProvider(string @namespace, IEnumerable<Type> types)
			: this(@namespace, types, null, null)
		{ }

		/// <summary>
		/// Creates a new <see cref="StronglyTypesGraphContext"/> based on the specified types
		/// and also including properties declared on the specified base types.
		/// </summary>
		/// <param name="types">The types to create graph types from</param>
		/// <param name="baseTypes">The base types that contain properties to include on graph types</param>
		/// <param name="extensionFactory">The factory to use to create extensions for new graph instances</param>
		public StronglyTypedGraphTypeProvider(string @namespace, IEnumerable<Type> types, IEnumerable<Type> baseTypes, Func<GraphInstance, object> extensionFactory)
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

			// Track the extension factory to use when creating new instances
			this.extensionFactory = extensionFactory;
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
		/// <param name="isBoundary">Indicates whether the property crosses scoping boundaries and should not be actively tracked</param>
		/// <param name="propertyType">The <see cref="GraphType"/> of the property</param>
		/// <param name="isList">Indicates whether the property represents a list of references or a single reference</param>
		/// <param name="attributes">The attributes assigned to the property</param>
		protected virtual GraphReferenceProperty CreateReferenceProperty(GraphType declaringType, PropertyInfo property, string name, bool isStatic, bool isBoundary, GraphType propertyType, bool isList, Attribute[] attributes)
		{
			return new PropertyInfoReferenceProperty(declaringType, property, name, isStatic, isBoundary, propertyType, isList, attributes);
		}

		/// <summary>
		/// Adds a property to the specified <see cref="GraphType"/> that represents an
		/// strongly-typed value value with the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="propertyType">The <see cref="Type"/> of the property</param>
		/// <param name="converter">The optional value type converter to use</param>
		/// <param name="attributes">The attributes assigned to the property</param>
		protected virtual GraphValueProperty CreateValueProperty(GraphType declaringType, PropertyInfo property, string name, bool isStatic, Type propertyType, TypeConverter converter, Attribute[] attributes)
		{
			return new PropertyInfoValueProperty(declaringType, property, name, isStatic, propertyType, converter, attributes);
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
			return supportedTypes.Contains(instance.GetType()) ? @namespace + instance.GetType().Name : null;
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
			return CreateGraphType(@namespace, type, extensionFactory);
		}

		/// <summary>
		/// Allows subclasses to create specific subclasses of <see cref="StrongGraphType"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="extensionFactory"></param>
		/// <returns></returns>
		protected abstract StrongGraphType CreateGraphType(string @namespace, Type type, Func<GraphInstance, object> extensionFactory);
		
		#endregion

		#region StrongGraphType

		/// <summary>
		/// Concrete subclass of <see cref="GraphType"/> that represents a specific <see cref="Type"/>.
		/// </summary>
		[Serializable]
		protected abstract class StrongGraphType : GraphType
		{
			protected internal StrongGraphType(string @namespace, Type type, Func<GraphInstance, object> extensionFactory)
				: base(@namespace + type.Name, type.AssemblyQualifiedName, GetBaseType(type), type.GetCustomAttributes(true).Cast<Attribute>().ToArray(), extensionFactory)
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
				StronglyTypedGraphTypeProvider provider = (StronglyTypedGraphTypeProvider)Provider;

				Type listItemType;

				// Determine if any inherited properties on the type have been replaced using the new keyword
				Dictionary<string, bool> isNewProperty = new Dictionary<string, bool>();
				foreach (PropertyInfo property in UnderlyingType.GetProperties())
					isNewProperty[property.Name] = isNewProperty.ContainsKey(property.Name);

				// Process all properties on the instance type to create references.  Process static properties
				// last since they would otherwise complicate calculated indexes when dealing with sub types.
				foreach (PropertyInfo property in UnderlyingType.GetProperties().OrderBy(p => (p.GetGetMethod(true) ?? p.GetSetMethod(true)).IsStatic))
				{
					// Exit immediately if the property was not in the list of valid declaring types
					//TODO: this fails if the type is managed in a separate provider.
					if (GraphContext.Current.GetGraphType(property.DeclaringType) == null || (isNewProperty[property.Name] && property.DeclaringType != UnderlyingType))
						continue;

					// Copy properties inherited from base graph types
					if (BaseType != null && BaseType.Properties.Contains(property.Name) && !isNewProperty[property.Name])
						AddProperty(BaseType.Properties[property.Name]);

					// Create references based on properties that relate to other instance types
					else if (provider.supportedTypes.Contains(property.PropertyType))
					{
						GraphReferenceProperty reference = provider.CreateReferenceProperty(this, property, property.Name, property.GetGetMethod().IsStatic, property.GetGetMethod().IsStatic, Context.GetGraphType(property.PropertyType), false, property.GetCustomAttributes(true).Cast<Attribute>().ToArray());
						if (reference != null)
							AddProperty(reference);
					}

					// Create references based on properties that are lists of other instance types
					else if (TryGetListItemType(property.PropertyType, out listItemType) && provider.supportedTypes.Contains(listItemType))
					{
						GraphReferenceProperty reference = provider.CreateReferenceProperty(this, property, property.Name, property.GetGetMethod().IsStatic, property.GetGetMethod().IsStatic, Context.GetGraphType(listItemType), true, property.GetCustomAttributes(true).Cast<Attribute>().ToArray());
						if (reference != null)
							AddProperty(reference);
					}

					// Create values for all other properties
					else
					{
						GraphValueProperty value = provider.CreateValueProperty(this, property, property.Name, property.GetGetMethod().IsStatic, property.PropertyType, TypeDescriptor.GetConverter(property.PropertyType), property.GetCustomAttributes(true).Cast<Attribute>().ToArray());
						if (value != null)
							AddProperty(value);
					}
				}
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
					return (IList) list;

				if (list is IListSource)
					return ((IListSource) list).GetList();

				if (property is PropertyInfoReferenceProperty)
					return null;

				// Add ICollection<T> support

				throw new NotSupportedException("Unable to convert the specified list instance into a valid IList implementation.");
			}

			/// <summary>
			/// Gets the item type of a list type, or returns false if the type is not a supported list type<TItem>
			/// </summary>
			/// <param name="listType"></param>
			/// <param name="itemType"></param>
			/// <returns></returns>
			protected virtual bool TryGetListItemType(Type listType, out Type itemType)
			{
				// First see if the type implements ICollection<T>
				foreach (Type interfaceType in listType.GetInterfaces())
				{
					if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(ICollection<>))
					{
						itemType = interfaceType.GetGenericArguments()[0];
						return true;
					}
				}

				// First see if the type implements IList and has a strongly-typed Item property indexed by an integer value
				if (typeof(IList).IsAssignableFrom(listType))
				{
					PropertyInfo itemProperty = listType.GetProperty("Item", new Type[] { typeof(int) });
					if (itemProperty != null)
					{
						itemType = itemProperty.PropertyType;
						return true;
					}
				}

				// Return false to indicate that the specified type is not a supported list type
				itemType = null;
				return false;
			}

			protected internal override void OnStartTrackingList(GraphInstance instance, GraphReferenceProperty property, IList list)
			{
				if (list is INotifyCollectionChanged)
					(new ListChangeEventAdapter(instance, property, (INotifyCollectionChanged) list)).Start();
				else
					base.OnStartTrackingList(instance, property, list);
			}

			protected internal override void OnStopTrackingList(GraphInstance instance, GraphReferenceProperty property, IList list)
			{
				if (list is INotifyCollectionChanged)
					(new ListChangeEventAdapter(instance, property, (INotifyCollectionChanged) list)).Stop();
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
					((StrongGraphType)instance.Type).OnListChanged(instance, property,
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

		#region PropertyInfoValueProperty

		[Serializable]
		protected class PropertyInfoValueProperty : GraphValueProperty
		{
			protected internal PropertyInfoValueProperty(GraphType declaringType, PropertyInfo property, string name, bool isStatic, Type propertyType, TypeConverter converter, Attribute[] attributes)
				: base(declaringType, name, isStatic, propertyType, converter, attributes)
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

		#region PropertyInfoReferenceProperty

		[Serializable]
		protected class PropertyInfoReferenceProperty : GraphReferenceProperty
		{
			protected internal PropertyInfoReferenceProperty(GraphType declaringType, PropertyInfo property, string name, bool isStatic, bool isBoundary, GraphType propertyType, bool isList, Attribute[] attributes)
				: base(declaringType, name, isStatic, isBoundary, propertyType, isList, attributes)
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