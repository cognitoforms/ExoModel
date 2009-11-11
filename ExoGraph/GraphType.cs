using System;
using System.Collections.Generic;

namespace ExoGraph
{
	/// <summary>
	/// Represents a specific type in a graph hierarchy.
	/// </summary>
	public class GraphType
	{
		#region Fields

		string name;
		GraphContext context;
		GraphType baseType;
		GraphTypeList subTypes = new GraphTypeList();
		GraphPropertyList properties = new GraphPropertyList();
		IList<GraphReferenceProperty> inReferences = new List<GraphReferenceProperty>();
		GraphReferencePropertyList outReferences = new GraphReferencePropertyList();
		GraphValuePropertyList values = new GraphValuePropertyList();
		Dictionary<Type, object> domainEvents = new Dictionary<Type, object>();
		GraphPathList paths = new GraphPathList();

		#endregion

		#region Contructors

		public GraphType(GraphContext context, string name)
		{
			this.context = context;
			this.name = name;
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

		/// <summary>
		/// Defines the delegate the custom event handlers must implement to subscribe.
		/// </summary>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="instance"></param>
		/// <param name="event"></param>
		public delegate void CustomEvent<TEvent>(GraphInstance instance, TEvent @event);

		/// <summary>
		/// Adds a domain event handler for a specific domain event raised by the current graph type.
		/// </summary>
		/// <typeparam name="TDomainEvent">
		/// The type of the domain event parameter that will be passed
		/// as an argument when the domain event is raised
		/// </typeparam>
		/// <param name="handler">The event handler for the domain event</param>
		public void Subscribe<TEvent>(CustomEvent<TEvent> handler)
		{
			object currentHandler;
			if (domainEvents.TryGetValue(typeof(TEvent), out currentHandler))
				domainEvents[typeof(TEvent)] = (CustomEvent<TEvent>)currentHandler + handler;
			else
				domainEvents[typeof(TEvent)] = handler;
		}

		/// <summary>
		/// Removes a domain event handler for a specific domain event raised by the current graph type.
		/// </summary>
		/// <typeparam name="TDomainEvent">
		/// The type of the domain event parameter that will be passed
		/// as an argument when the domain event is raised
		/// </typeparam>
		/// <param name="handler">The event handler for the domain event</param>
		public void Unsubscribe<TEvent>(CustomEvent<TEvent> handler)
		{
			object currentHandler;
			if (domainEvents.TryGetValue(typeof(TEvent), out currentHandler))
				domainEvents[typeof(TEvent)] = (CustomEvent<TEvent>)currentHandler - handler;
		}

		/// <summary>
		/// Raises any domain events registered for the specified domain event type.
		/// </summary>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="domainEvent"></param>
		internal void RaiseEvent<TEvent>(GraphCustomEvent<TEvent> customEvent)
		{
			object currentHandler;
			if (domainEvents.TryGetValue(typeof(TEvent), out currentHandler))
				((CustomEvent<TEvent>)currentHandler)(customEvent.Instance, customEvent.CustomEvent);
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
			return context.GetInstance(context.CreateInstance(this, null));
		}

		public GraphInstance Create(string id)
		{
			return context.GetInstance(context.CreateInstance(this, id));
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

		#endregion
	}
}
