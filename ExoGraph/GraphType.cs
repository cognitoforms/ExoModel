using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ExoGraph
{
	/// <summary>
	/// Represents a specific type in a graph hierarchy.
	/// </summary>
	[DataContract]
	public class GraphType
	{
		#region Fields

		string name;
		string qualifiedName;
		GraphContext context;
		GraphType baseType;
		GraphTypeList subTypes = new GraphTypeList();
		GraphPropertyList properties = new GraphPropertyList();
		IList<GraphReferenceProperty> inReferences = new List<GraphReferenceProperty>();
		GraphReferencePropertyList outReferences = new GraphReferencePropertyList();
		GraphValuePropertyList values = new GraphValuePropertyList();
		Dictionary<Type, object> customEvents = new Dictionary<Type, object>();
		Dictionary<Type, object> transactedCustomEvents = new Dictionary<Type, object>();
		GraphPathList paths = new GraphPathList();
		Attribute[] attributes;
		int nextPropertyIndex;

		#endregion

		#region Contructors

		internal GraphType(GraphContext context, string name, string qualifiedName, Attribute[] attributes)
		{
			this.context = context;
			this.name = name;
			this.qualifiedName = qualifiedName;
			this.attributes = attributes;
		}

		#endregion

		#region Properties

		public GraphContext Context
		{
			get
			{
				return context;
			}
		}

		public string Name
		{
			get
			{
				return name;
			}
		}

		public string QualifiedName
		{
			get
			{
				return qualifiedName;
			}
		}

		public GraphType BaseType
		{
			get
			{
				return baseType;
			}
		}

		public GraphTypeList SubTypes
		{
			get
			{
				return subTypes;
			}
		}

		public GraphPropertyList Properties
		{
			get
			{
				return properties;
			}
		}

		internal IEnumerable<GraphReferenceProperty> InReferences
		{
			get
			{
				return inReferences;
			}
		}

		internal GraphReferencePropertyList OutReferences
		{
			get
			{
				return outReferences;
			}
		}

		internal GraphValuePropertyList Values
		{
			get
			{
				return values;
			}
		}

		internal GraphPathList Paths
		{
			get
			{
				return paths;
			}
		}

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
		/// <typeparam name="TDomainEvent">
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
				((CustomEvent<TEvent>)currentHandler)(customEvent.Instance, customEvent.CustomEvent);
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
			// First see if the path has already been created for this instance type
			GraphPath p = Paths[path];
			if (p != null)
				return p;

			// Otherwise, create and cache a new path
			p = new GraphPath(this, path);
			Paths.Add(p);

			return p;
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

		public GraphInstance Create()
		{
			return context.GetGraphInstance(context.GetInstance(this, null));
		}

		public GraphInstance Create(string id)
		{
			return context.GetGraphInstance(context.GetInstance(this, id));
		}

		/// <summary>
		/// Returns the name of the graph type.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return name;
		}


		/// <summary>
		/// Gets the <see cref="GraphInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		public GraphInstance GetReference(string property)
		{
			object reference = Context.GetProperty(null, property);
			if (reference != null)
				return Context.GetGraphInstance(reference);
			return null;
		}

		/// <summary>
		/// Gets the <see cref="GraphInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphReferenceProperty"/></param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		public GraphInstance GetReference(GraphReferenceProperty property)
		{
			return GetReference(property.Name);
		}

		/// <summary>
		/// Gets the value assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The value of the property</returns>
		public object GetValue(string property)
		{
			return Context.GetProperty(null, property);
		}

		/// <summary>
		/// Gets the value assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphValueProperty"/></param>
		/// <returns>The value of the property</returns>
		public object GetValue(GraphValueProperty property)
		{
			return GetValue(property.Name);
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
			Context.SetProperty(null, property, value == null ? null : value.Instance);
		}

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		public void SetReference(GraphReferenceProperty property, GraphInstance value)
		{
			SetReference(property.Name, value);
		}

		/// <summary>
		/// Sets a property to the specified value.
		/// </summary>
		/// <param name="property">The property to set</param>
		/// <param name="value">The value of the property</param>
		public void SetValue(string property, object value)
		{
			Context.SetProperty(null, property, value);
		}

		/// <summary>
		/// Sets a property to the specified value.
		/// </summary>
		/// <param name="property">The property to set</param>
		/// <param name="value">The value of the property</param>
		public void SetValue(GraphValueProperty property, object value)
		{
			SetValue(property.Name, value);
		}

		/// <summary>
		/// Sets the base <see cref="GraphType"/> for the current instance and 
		/// adds the current instance the list of sub types for the base type.
		/// </summary>
		/// <param name="baseType"></param>
		internal void SetBaseType(GraphType baseType)
		{
			if (this.baseType != null)
				throw new InvalidOperationException("The base type of a graph type cannot be changed once it has been set.");

			this.baseType = baseType;
			baseType.subTypes.Add(this);
			nextPropertyIndex = baseType.nextPropertyIndex - 1;
		}

		/// <summary>
		/// Adds the specified property to the current graph type.
		/// </summary>
		/// <param name="property"></param>
		internal void AddProperty(GraphProperty property)
		{
			if (property is GraphReferenceProperty)
			{
				outReferences.Add((GraphReferenceProperty)property);
				((GraphReferenceProperty)property).PropertyType.inReferences.Add((GraphReferenceProperty)property);
			}
			else
				values.Add((GraphValueProperty)property);

			properties.Add(property);
		}

		internal int GetNextPropertyIndex()
		{
			return nextPropertyIndex++;
		}

		#endregion
	}
}
