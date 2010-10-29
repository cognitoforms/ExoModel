using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Collections;
using System.Reflection;

namespace ExoGraph
{
	/// <summary>
	/// Represents a specific type in a graph hierarchy.
	/// </summary>
	[DataContract]
    [Serializable]
	public abstract class GraphType : ISerializable, IGraphPropertySource
	{
		#region Fields

		internal static GraphType Unknown = new UnknownGraphType();

		Dictionary<Type, object> customEvents = new Dictionary<Type, object>();
		Dictionary<Type, object> transactedCustomEvents = new Dictionary<Type, object>();
		Attribute[] attributes;
		Dictionary<Type, object> extensions;

		#endregion

		#region Contructors

		public GraphType(string name, string qualifiedName, GraphType baseType, Attribute[] attributes)
		{
			this.Name = name;
			this.QualifiedName = qualifiedName;
			this.attributes = attributes;

			if (baseType != null)
			{
				this.BaseType = baseType;
				baseType.SubTypes.Add(this);
			}

			// Initialize list properties
			this.SubTypes = new GraphTypeList();
			this.Properties = new GraphPropertyList();
			this.Methods = new GraphMethodList();
			this.InReferences = new List<GraphReferenceProperty>();
			this.OutReferences = new GraphReferencePropertyList();
			this.Values = new GraphValuePropertyList();
			this.Paths = new GraphPathList();
		}

		#endregion

		#region Properties

		public GraphContext Context { get; private set;  }

		public string Name { get; private set; }

		public string QualifiedName { get; private set; }

		public GraphType BaseType { get; private set; }

		public GraphTypeList SubTypes { get; private set; }

		public GraphPropertyList Properties { get; private set; }

		public GraphMethodList Methods { get; private set; }

		internal IEnumerable<GraphReferenceProperty> InReferences { get; private set; }

		internal GraphReferencePropertyList OutReferences { get; private set; }

		internal GraphValuePropertyList Values { get; private set; }

		internal GraphPathList Paths { get; private set; }

		internal int PropertyCount { get; private set; }

		protected internal IGraphTypeProvider Provider { get; internal set; }

		#endregion

		#region Events

		public event EventHandler<GraphInitEvent> Init;
		public event EventHandler<GraphPropertyGetEvent> PropertyGet;
		public event EventHandler<GraphReferenceChangeEvent> ReferenceChange;
		public event EventHandler<GraphValueChangeEvent> ValueChange;
		public event EventHandler<GraphListChangeEvent> ListChange;
		public event EventHandler<GraphSaveEvent> Save;

		#endregion

		#region Methods

		/// <summary>
		/// Gets or creates an extension instance linked to the current <see cref="GraphType"/>.
		/// </summary>
		/// <typeparam name="TExtension">The type of extension to create.</typeparam>
		/// <returns></returns>
		public TExtension GetExtension<TExtension>()
			where TExtension : class, new()
		{
			object extension;
			if (extensions == null)
				extensions = new Dictionary<Type, object>();
			if (!extensions.TryGetValue(typeof(TExtension), out extension))
				extensions[typeof(TExtension)] = extension = new TExtension();
			return (TExtension)extension;
		}

		/// <summary>
		/// Gets the item type of a list type, or returns false if the type is not a supported list type.
		/// </summary>
		/// <param name="listType"></param>
		/// <param name="itemType"></param>
		/// <returns></returns>
		protected internal virtual bool TryGetListItemType(Type listType, out Type itemType)
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

		/// <summary>
		/// Performs one time initialization on the <see cref="GraphType"/> when it is registered
		/// with the <see cref="GraphContext"/>.
		/// </summary>
		/// <param name="context"></param>
		internal void Initialize(GraphContext context)
		{
			// Set the context the graph type is registered with
			this.Context = context;

			// Set the next property index for properties added inside OnInit
			PropertyCount = BaseType == null ? 0 : BaseType.PropertyCount;

			// Allow subclasses to perform initialization, such as added properties
			OnInit();
		}

		/// <summary>
		/// Overriden by subclasses to perform type initialization, specifically including
		/// setting the base type and adding properties.  This initialization must occur inside this
		/// method and not in the constructor to ensure that base types are completely initialized before
		/// their child types.
		/// </summary>
		protected internal abstract void OnInit();

		/// <summary>
		/// Adds the specified property to the current graph type.
		/// </summary>
		/// <param name="property"></param>
		protected void AddProperty(GraphProperty property)
		{
			if (property.DeclaringType == this)
				property.Index = PropertyCount++;

			if (property is GraphReferenceProperty)
			{
				OutReferences.Add((GraphReferenceProperty)property);
				((List<GraphReferenceProperty>)((GraphReferenceProperty)property).PropertyType.InReferences).Add((GraphReferenceProperty)property);
			}
			else
				Values.Add((GraphValueProperty)property);

			Properties.Add(property);
		}

		/// <summary>
		/// Adds the specified method to the current graph type.
		/// </summary>
		/// <param name="method"></param>
		protected void AddMethod(GraphMethod method)
		{
			Methods.Add(method);
		}

		internal void RaiseInit(GraphInitEvent initEvent)
		{
			if (Init != null)
				Init(this, initEvent);
		}

		internal void RaisePropertyGet(GraphPropertyGetEvent propertyGetEvent)
		{
			if (PropertyGet != null)
				PropertyGet(this, propertyGetEvent);
		}

		internal void RaiseReferenceChange(GraphReferenceChangeEvent referenceChangeEvent)
		{
			if (ReferenceChange != null)
				ReferenceChange(this, referenceChangeEvent);
		}

		internal void RaiseValueChange(GraphValueChangeEvent valueChangeEvent)
		{
			if (ValueChange != null)
				ValueChange(this, valueChangeEvent);
		}

		internal void RaiseListChange(GraphListChangeEvent listChangeEvent)
		{
			if (ListChange != null)
				ListChange(this, listChangeEvent);
		}

		internal void RaiseSave(GraphSaveEvent graphSaveEvent)
		{
			if (Save != null)
				Save(this, graphSaveEvent);
		}

		/// <summary>
		/// Defines the delegate the custom event handlers must implement to subscribe.
		/// </summary>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="instance"></param>
		/// <param name="event"></param>
		public delegate void CustomEvent<TEvent>(GraphInstance instance, TEvent @event);

		/// <summary>
		/// Adds a custom event handler for a specific custom event raised by the current graph type.
		/// </summary>
		/// <typeparam name="TEvent">
		/// The type of the custom event parameter that will be passed
		/// as an argument when the custom event is raised
		/// </typeparam>
		/// <param name="handler">The event handler for the custom event</param>
		public void Subscribe<TEvent>(CustomEvent<TEvent> handler)
		{
			object currentHandler;

			if (typeof(ITransactedGraphEvent).IsAssignableFrom(typeof(TEvent)))
				transactedCustomEvents[typeof(TEvent)] =
					transactedCustomEvents.TryGetValue(typeof(TEvent), out currentHandler) ?
					(CustomEvent<TEvent>)currentHandler + handler : handler;
			else
				customEvents[typeof(TEvent)] =
						customEvents.TryGetValue(typeof(TEvent), out currentHandler) ?
						(CustomEvent<TEvent>)currentHandler + handler : handler;
		}

		/// <summary>
		/// Removes a custom event handler for a specific custom event raised by the current graph type.
		/// </summary>
		/// <typeparam name="TDomainEvent">
		/// The type of the custom event parameter that will be passed
		/// as an argument when the domain event is raised
		/// </typeparam>
		/// <param name="handler">The event handler for the custom event</param>
		public void Unsubscribe<TEvent>(CustomEvent<TEvent> handler)
		{
			object currentHandler;
			if (typeof(ITransactedGraphEvent).IsAssignableFrom(typeof(TEvent)))
			{
				if (transactedCustomEvents.TryGetValue(typeof(TEvent), out currentHandler))
					transactedCustomEvents[typeof(TEvent)] = (CustomEvent<TEvent>)currentHandler - handler;
			}
			else
			{
				if (customEvents.TryGetValue(typeof(TEvent), out currentHandler))
					customEvents[typeof(TEvent)] = (CustomEvent<TEvent>)currentHandler - handler;
			}
		}

		/// <summary>
		/// Raises any domain events registered for the specified domain event type.
		/// </summary>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="domainEvent"></param>
		internal void RaiseEvent<TEvent>(GraphCustomEvent<TEvent> customEvent)
		{
			object currentHandler;
			if (customEvents.TryGetValue(typeof(TEvent), out currentHandler))
				((CustomEvent<TEvent>) currentHandler)(customEvent.Instance, customEvent.CustomEvent);
			else if (this.BaseType != null)
				this.BaseType.RaiseEvent<TEvent>(customEvent);
		}

		/// <summary>
		/// Indicates whether the current type has one or more attributes of the specified type.
		/// </summary>
		/// <typeparam name="TAttribute"></typeparam>
		/// <returns></returns>
		public bool HasAttribute<TAttribute>()
			where TAttribute : Attribute
		{
			return GetAttributes<TAttribute>().Length > 0;
		}

		/// <summary>
		/// Returns an array of attributes defined on the current type.
		/// </summary>
		/// <typeparam name="TAttribute"></typeparam>
		/// <returns></returns>
		public TAttribute[] GetAttributes<TAttribute>()
			where TAttribute : Attribute
		{
			List<TAttribute> matches = new List<TAttribute>();

			// Find matching attributes on the current property
			foreach (Attribute attribute in attributes)
			{
				if (attribute is TAttribute)
					matches.Add((TAttribute)attribute);
			}

			return matches.ToArray();
		}

		/// <summary>
		/// Gets the <see cref="GraphPath"/> starting from the current <see cref="GraphType"/> based
		/// on the specified path string.
		/// </summary>
		/// <param name="path"></param>
		/// <returns>The requested <see cref="GraphPath"/></returns>
		public GraphPath GetPath(string path)
		{
			GraphPath graphPath;
			if (!TryGetPath(path, out graphPath))
				throw new ArgumentException("The specific path could not be evaluated: " + path);
			return graphPath;
		}

		/// <summary>
		///  Gets the <see cref="GraphPath"/> starting from the current <see cref="GraphType"/> based
		/// on the specified path string.
		/// </summary>
		/// <param name="path"></param>
		/// <param name="graphPath"></param>
		/// <returns>True if the path is valid and was returned as an output parameter, otherwise false.</returns>
		public bool TryGetPath(string path, out GraphPath graphPath)
		{
			// First see if the path has already been created for this instance type
			graphPath = Paths[path];
			if (graphPath != null)
				return true;

			// Otherwise, create and cache a new path
			graphPath = GraphPath.CreatePath(this, path);
			if (graphPath == null)
				return false;

			Paths.Add(graphPath);
			return true;
		}

		/// <summary>
		/// Indicates whether the specified <see cref="GraphInstance"/> is either of the current type
		/// or of a sub type of the current type.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public bool IsInstanceOfType(GraphInstance instance)
		{
			GraphType instanceType = instance.Type;
			GraphType currentType = this;
			while (instanceType != null)
			{
				if (instanceType == currentType)
					return true;
				instanceType = instanceType.BaseType;
			}
			return false;
		}

		/// <summary>
		/// Creates a new instance of the current <see cref="GraphType"/>.
		/// </summary>
		/// <returns></returns>
		public GraphInstance Create()
		{
			return GetGraphInstance(GetInstance(null));
		}

		/// <summary>
		/// Creates an existing instance of the current <see cref="GraphType"/>.
		/// </summary>
		/// <returns></returns>
		public GraphInstance Create(string id)
		{
			object instance = GetInstance(id);
			return instance == null ? null : GetGraphInstance(instance);
		}

		/// <summary>
		/// Returns the name of the graph type.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Name;
		}


		/// <summary>
		/// Gets the <see cref="GraphInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		public GraphInstance GetReference(string property)
		{
			return GetReference(OutReferences[property]);
		}

		/// <summary>
		/// Gets the <see cref="GraphInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphReferenceProperty"/></param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		public GraphInstance GetReference(GraphReferenceProperty property)
		{
			object reference = property.GetValue(null);
			if (reference != null)
				return GetGraphInstance(reference);
			return null;
		}

		/// <summary>
		/// Gets the value assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The value of the property</returns>
		public object GetValue(string property)
		{
			return GetValue(Values[property]);
		}

		/// <summary>
		/// Gets the value assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphValueProperty"/></param>
		/// <returns>The value of the property</returns>
		public object GetValue(GraphValueProperty property)
		{
			if (property.AutoConvert)
				return property.Converter.ConvertTo(property.GetValue(null), typeof(object));
			else
				return property.GetValue(null);
		}

		/// <summary>
		/// Gets the list of <see cref="GraphInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of property</param>
		/// <returns>The list of instances</returns>
		public GraphInstanceList GetList(string property)
		{
			return GetList(OutReferences[property]);
		}

		/// <summary>
		/// Gets the list of <see cref="GraphInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphReferenceProperty"/></param>
		/// <returns>The list of instances</returns>
		public GraphInstanceList GetList(GraphReferenceProperty property)
		{
			return new GraphInstanceList(null, property);
		}

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		public void SetReference(string property, GraphInstance value)
		{
			SetReference(OutReferences[property], value);
		}

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		public void SetReference(GraphReferenceProperty property, GraphInstance value)
		{
			property.SetValue(null, value == null ? null : value.Instance);
		}

		/// <summary>
		/// Sets a property to the specified value.
		/// </summary>
		/// <param name="property">The property to set</param>
		/// <param name="value">The value of the property</param>
		public void SetValue(string property, object value)
		{
			SetValue(Values[property], value);
		}

		/// <summary>
		/// Sets a property to the specified value.
		/// </summary>
		/// <param name="property">The property to set</param>
		/// <param name="value">The value of the property</param>
		public void SetValue(GraphValueProperty property, object value)
		{
			if (property.AutoConvert)
				property.SetValue(null, property.Converter.ConvertFrom(value));
			else
				property.SetValue(null, value);
		}

		/// <summary>
		/// Gets or sets the value of the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The underlying value of the property in the physical graph</returns>
		public object this[string property]
		{
			get
			{
				return this[Properties[property]];
			}
			set
			{
				this[Properties[property]] = value;
			}
		}

		/// <summary>
		/// Gets or sets the value of the specified property.
		/// </summary>
		/// <param name="property">The <see cref="GraphProperty"/> to get or set</param>
		/// <returns>The underlying value of the property in the physical graph</returns>
		public object this[GraphProperty property]
		{
			get
			{
				return property is GraphValueProperty ? GetValue((GraphValueProperty)property) : property.GetValue(null);
			}
			set
			{
				if (property is GraphValueProperty)
					SetValue((GraphValueProperty)property, value);
				else
					property.SetValue(null, value);
			}
		}

		protected void OnPropertyGet(GraphInstance instance, string property)
		{
			OnPropertyGet(instance, instance.Type.Properties[property]);
		}

		protected void OnPropertyGet(GraphInstance instance, GraphProperty property)
		{
			// Static notifications are not supported
			if (property.IsStatic)
				return;

			GraphEvent propertyGet = new GraphPropertyGetEvent(instance, property);

			propertyGet.Notify();
		}

		/// <summary>
		/// Converts the specified object into a instance that implements <see cref="IList"/>.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="list"></param>
		/// <returns></returns>
		protected internal abstract IList ConvertToList(GraphReferenceProperty property, object list);
	
		protected internal virtual void OnStartTrackingList(GraphInstance instance, GraphReferenceProperty property, IList list)
		{
		}

		protected internal virtual void OnStopTrackingList(GraphInstance instance, GraphReferenceProperty property, IList list)
		{
		}

		protected internal void OnPropertyChanged(GraphInstance instance, string property, object oldValue, object newValue)
		{
			OnPropertyChanged(instance, instance.Type.Properties[property], oldValue, newValue);
		}

		protected internal void OnPropertyChanged(GraphInstance instance, GraphProperty property, object oldValue, object newValue)
		{
			// Check to see what type of property was changed
			if (property is GraphReferenceProperty)
			{
				GraphReferenceProperty reference = (GraphReferenceProperty) property;

				// Changes to list properties should call OnListChanged. However, some implementations
				// may allow setting lists, so this case must be handled appropriately.
				if (reference.IsList)
				{
					// Notify the context that the items in the old list have been removed
					if (oldValue != null)
					{
						var oldList = ConvertToList(reference, oldValue);
						OnListChanged(instance, reference, null, oldList);
						OnStopTrackingList(instance, reference, oldList);
					}

					// Then notify the context that the items in the new list have been added
					if (newValue != null)
					{
						var newList = ConvertToList(reference, newValue);
						OnListChanged(instance, reference, newList, null);
						OnStartTrackingList(instance, reference, newList);
					}
				}

				// Notify subscribers that a reference property has changed
				else
					new GraphReferenceChangeEvent(
						instance, (GraphReferenceProperty) property,
						oldValue == null ? null : GetGraphInstance(oldValue),
						newValue == null ? null : GetGraphInstance(newValue)
					).Notify();
			}

			// Otherwise, notify subscribers that a value property has changed
			else
				new GraphValueChangeEvent(instance, (GraphValueProperty) property, oldValue, newValue).Notify();
		}

		protected void OnListChanged(GraphInstance instance, string property, IEnumerable added, IEnumerable removed)
		{
			OnListChanged(instance, (GraphReferenceProperty) instance.Type.Properties[property], added, removed);
		}

		protected void OnListChanged(GraphInstance instance, GraphReferenceProperty property, IEnumerable added, IEnumerable removed)
		{
			// Create a new graph list change event and notify subscribers
			new GraphListChangeEvent(instance, property, EnumerateInstances(added), EnumerateInstances(removed)).Notify();
		}

		IEnumerable<GraphInstance> EnumerateInstances(IEnumerable items)
		{
			if (items != null)
				foreach (object instance in items)
					yield return GetGraphInstance(instance);
		}

		/// <summary>
		/// Called by subclasses to notify the context that a commit has occurred.
		/// </summary>
		/// <param name="instance"></param>
		protected void OnSave(GraphInstance instance)
		{
			new GraphSaveEvent(instance).Notify();
		}

		/// <summary>
		/// Saves changes to the specified instance and related instances in the graph.
		/// </summary>
		/// <param name="graphInstance"></param>
		protected internal abstract void SaveInstance(GraphInstance graphInstance);
	
		public abstract GraphInstance GetGraphInstance(object instance);
		
		protected internal abstract string GetId(object instance);
		
		protected internal abstract object GetInstance(string id);

		protected internal abstract void DeleteInstance(GraphInstance graphInstance);

		#endregion

		#region ISerializable Members
		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.SetType(typeof(Serialized));
			info.AddValue("name", Name);
		}

		[Serializable]
		class Serialized : ISerializable, IObjectReference
		{
			string typeName;

			#region ISerializable Members
			public Serialized(SerializationInfo info, StreamingContext context)
			{
				typeName = info.GetString("name");
			}

			void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
			{
				throw new NotImplementedException("this code should never run");
			}
			#endregion

			#region IObjectReference Members
			public object GetRealObject(StreamingContext context)
			{
                 return typeName == "ExoGraph.GraphType.Unknown" ? GraphType.Unknown : GraphContext.Current.GetGraphType(typeName);
            }
			#endregion
		}
		#endregion

		#region UnknownGraphType
       [Serializable]
		class UnknownGraphType : GraphType
		{
			internal UnknownGraphType()
               : base("ExoGraph.GraphType.Unknown", "ExoGraph.GraphType.Unknown", null, new Attribute[] { })
			{ }

			protected internal override void OnInit()
			{
				throw new NotSupportedException();
			}

			protected internal override IList ConvertToList(GraphReferenceProperty property, object list)
			{
				throw new NotSupportedException();
			}

			protected internal override void SaveInstance(GraphInstance graphInstance)
			{
				throw new NotSupportedException();
			}

			public override GraphInstance GetGraphInstance(object instance)
			{
				throw new NotSupportedException();
			}

			protected internal override string GetId(object instance)
			{
				throw new NotSupportedException();
			}

			protected internal override object GetInstance(string id)
			{
				throw new NotSupportedException();
			}

			protected internal override void DeleteInstance(GraphInstance graphInstance)
			{
				throw new NotSupportedException();
			}
		}

		#endregion
	}
}
