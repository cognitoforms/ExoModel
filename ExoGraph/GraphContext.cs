using System;
using System.Collections.Generic;
using System.Collections;

namespace ExoGraph
{
	/// <summary>
	/// Base class for context classes tracking the type information and events
	/// for a set of objects in graph.
	/// </summary>
	public abstract class GraphContext
	{
		#region Fields

		/// <summary>
		/// Tracks the types of objects in the graph.
		/// </summary>
		GraphTypeList graphTypes = new GraphTypeList();

		/// <summary>
		/// Tracks the next auto-generated id assigned to new instances.
		/// </summary>
		int nextId;

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the current graph context.
		/// </summary>
		public static GraphContext Current
		{
			get
			{
				return Provider.Context;
			}
			set
			{
				Provider.Context = value;
			}
		}

		/// <summary>
		/// Gets or sets the <see cref="IGraphContextProvider"/> provider responsible for
		/// creating and storing the <see cref="GraphContext"/> for the application.
		/// </summary>
		public static IGraphContextProvider Provider { get; set; }

		#endregion

		#region Events

		/// <summary>
		/// Allows subscribers to be notified of all <see cref="GraphEvent"/> occurrences
		/// raised within the current graph context.
		/// </summary>
		public event EventHandler<GraphEvent> Event;

		#endregion

		#region Graph Instance Methods

		/// <summary>
		/// Begins a transaction within the current graph context.
		/// </summary>
		/// <returns>The transaction instance</returns>
		/// <remarks>
		/// The transaction subscribes to graph events and should be used inside a using block
		/// to ensure that the subscriptions are eventually released.
		/// <see cref="GraphTransaction.Commit"/> must be called to ensure the transaction is not rolled back.
		/// <see cref="Rollback"/> may be called at any time to force the transaction to roll back.
		/// After <see cref="Commit"/> or <see cref="Rollback"/> occurs, further graph events
		/// will not be tracked by the transaction.
		/// </remarks>
		public GraphTransaction BeginTransaction()
		{
			return new GraphTransaction(this);
		}

		/// <summary>
		/// Called by each <see cref="GraphEvent"/> to notify the context that a graph event has occurred.
		/// </summary>
		/// <param name="graphEvent"></param>
		internal void Notify(GraphEvent graphEvent)
		{
			if (Event != null)
				Event(this, graphEvent);
		}

		/// <summary>
		/// Generates a unique identifier to assign to new instances that do not yet have an id.
		/// </summary>
		/// <returns></returns>
		internal string GenerateId()
		{
			return "?" + ++nextId;
		}

		/// <summary>
		/// Ensures that the numeric value incorporated in the specified id will not be reassigned
		/// to new instances.
		/// </summary>
		/// <param name="id"></param>
		internal void ReserveId(string id)
		{
			if (id != null)
			{
			}
		}

		/// <summary>
		/// Called by <see cref="Context"/> subclasses to obtain a <see cref="GraphInstance"/> for a 
		/// newly created graph object.  This also causes a graph change notification to occur notifying 
		/// subscribers that the new instance is now associated to the current graph context.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected GraphInstance OnInit(object instance)
		{
			// Create the new graph instance
			return new GraphInstance(GetGraphType(instance), instance);
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

		protected void OnPropertyChanged(GraphInstance instance, string property, object oldValue, object newValue)
		{
			OnPropertyChanged(instance, instance.Type.Properties[property], oldValue, newValue);
		}

		protected void OnPropertyChanged(GraphInstance instance, GraphProperty property, object oldValue, object newValue)
		{
			// Check to see what type of property was changed
			if (property is GraphReferenceProperty)
			{
				GraphReferenceProperty reference = (GraphReferenceProperty)property;

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
						var newList = ConvertToList(reference, oldValue);
						OnListChanged(instance, reference, newList, null);
						OnStopTrackingList(instance, reference, newList);
					}
				}

				// Notify subscribers that a reference property has changed
				else
					new GraphReferenceChangeEvent(
						instance, (GraphReferenceProperty)property,
						oldValue == null ? null : GetGraphInstance(oldValue), 
						newValue == null ? null : GetGraphInstance(newValue)
					).Notify();
			}

			// Otherwise, notify subscribers that a value property has changed
			else
				new GraphValueChangeEvent(instance, (GraphValueProperty)property, oldValue, newValue).Notify();
		}

		protected void OnListChanged(GraphInstance instance, string property, IEnumerable added, IEnumerable removed)
		{
			OnListChanged(instance, (GraphReferenceProperty)instance.Type.Properties[property], added, removed);
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
		protected internal abstract void Save(GraphInstance graphInstance);

		public abstract GraphInstance GetGraphInstance(object instance);

		protected internal abstract GraphType GetGraphType(object instance);

		protected internal abstract string GetId(object instance);

		protected internal abstract object GetInstance(GraphType type, string id);

		protected internal abstract void DeleteInstance(object instance);

		#endregion

		#region Graph Type Methods

		/// <summary>
		/// Gets the <see cref="GraphType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public GraphType GetGraphType(string typeName)
		{
			GraphType type = graphTypes[typeName];
			if (type == null)
			{
				type = CreateGraphType(typeName);
			}
			return type;
		}

		/// <summary>
		/// Gets the <see cref="GraphType"/> that corresponds to the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public abstract GraphType GetGraphType(Type type);

		/// <summary>
		/// Gets the <see cref="GraphType"/> that corresponds to TType.
		/// </summary>
		/// <typeparam name="TType"></typeparam>
		/// <returns></returns>
		public GraphType GetGraphType<TType>()
		{
			return GetGraphType(typeof(TType));
		}

		/// <summary>
		/// Creates a <see cref="GraphType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		protected abstract GraphType CreateGraphType(string typeName);

		/// <summary>
		/// Creates a new <see cref="GraphType"/> instance with the specified name
		/// and associates it with the current graph context.
		/// </summary>
		/// <param name="name">The unique name of the type</param>
		/// <param name="qualifiedName">The fully qualified name of the type</param>
		/// <param name="attributes">The attributes assigned to the type</param>
		/// <param name="extensionFactory">The factory to use to create extensions for new graph instances</param>
		/// <returns></returns>
		protected virtual GraphType CreateGraphType(string name, string qualifiedName, Attribute[] attributes, Func<GraphInstance, object> extensionFactory)
		{
			GraphType type = new GraphType(this, name, qualifiedName, attributes, extensionFactory);
			graphTypes.Add(type);
			return type;
		}

		/// <summary>
		/// Sets the base <see cref="GraphType"/> for the specified type.
		/// </summary>
		/// <param name="type">The type of the sub type</param>
		/// <param name="baseType">The type of the base type</param>
		protected virtual void SetBaseType(GraphType subType, GraphType baseType)
		{
			subType.SetBaseType(baseType);
		}

		/// <summary>
		/// Adds an existing property to the specified <see cref="GraphType"/> that is
		/// inherited from a base type.
		/// </summary>
		/// <param name="declaringType">The <see cref="GraphType"/> the property is for</param>
		/// <param name="property">The property to add</param>
		protected void AddProperty(GraphType declaringType, GraphProperty property)
		{
			declaringType.AddProperty(property);
		}

		#endregion
	}
}
