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

		/// <summary>
		/// Gets the list of <see cref="GraphType"/> instances that are defined
		/// for the current graph context.
		/// </summary>
		public GraphTypeList GraphTypes
		{
			get
			{
				return graphTypes;
			}
		}

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
			return new GraphTransaction(this, null);
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
			GraphEvent propertyGet = new GraphPropertyGetEvent(instance, property);

			propertyGet.Notify();
		}

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

		protected void OnPropertyChanged(GraphInstance instance, GraphProperty property, object originalValue, object currentValue)
		{
			// Check to see what type of property was changed
			if (property is GraphReferenceProperty)
			{
				GraphReferenceProperty reference = (GraphReferenceProperty)property;

				// Changes to list properties should call OnListChanged. However, some implementations
				// may allow setting lists, so this case must be handled appropriately.
				if (reference.IsList)
				{
					// Notify the context that the items in the original list have been removed
					if (originalValue is IList)
						OnListChanged(instance, reference, null, (IList)originalValue);

					// Then notify the context that the items in the new list have been added
					if (currentValue is IList)
						OnListChanged(instance, reference, (IList)currentValue, null);

					// Finally, notify subclasses that the list reference has changed in case they
					// are subscribing to list events to support tracking list changes
					OnStopTrackingList(instance, reference, (IList)originalValue);
					OnStopTrackingList(instance, reference, (IList)currentValue);
				}

				// Notify subscribers that a reference property has changed
				else
					new GraphReferenceChangeEvent(
						instance, (GraphReferenceProperty)property,
						originalValue == null ? null : GetGraphInstance(originalValue), 
						currentValue == null ? null : GetGraphInstance(currentValue)
					).Notify();
			}

			// Otherwise, notify subscribers that a value property has changed
			else
				new GraphValueChangeEvent(instance, (GraphValueProperty)property, originalValue, currentValue).Notify();
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

		protected internal abstract GraphInstance GetGraphInstance(object instance);

		protected internal abstract GraphType GetGraphType(object instance);

		protected internal abstract string GetId(object instance);

		protected internal abstract object GetInstance(GraphType type, string id);

		protected internal abstract object GetProperty(object instance, string property);

		protected internal abstract object GetProperty(GraphType type, string property);

		protected internal abstract void SetProperty(object instance, string property, object value);

		protected internal abstract void SetProperty(GraphType type, string property, object value);

		protected internal abstract void DeleteInstance(object instance);

		#endregion

		#region Graph Type Methods



		/// <summary>
		/// Creates a new <see cref="GraphType"/> instance with the specified name
		/// and associates it with the current graph context.
		/// </summary>
		/// <param name="name">The unique name of the type</param>
		/// <param name="qualifiedName">The fully qualified name of the type</param>
		/// <param name="attributes">The attributes assigned to the type</param>
		/// <returns></returns>
		protected virtual GraphType CreateGraphType(string name, string qualifiedName, Attribute[] attributes)
		{
			GraphType type = new GraphType(this, name, qualifiedName, attributes);
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
		/// Adds a property to the specified <see cref="GraphType"/> that represents an
		/// association with another <see cref="GraphType"/> instance.
		/// </summary>
		/// <param name="declaringType">The <see cref="GraphType"/> the property is for</param>
		/// <param name="name">The name of the property</param>
		/// <param name="isStatic">Indicates whether the property is statically defined on the type</param>
		/// <param name="isBoundary">Indicates whether the property crosses scoping boundaries and should not be actively tracked</param>
		/// <param name="propertyType">The <see cref="GraphType"/> of the property</param>
		/// <param name="isList">Indicates whether the property represents a list of references or a single reference</param>
		/// <param name="attributes">The attributes assigned to the property</param>
		protected virtual void AddProperty(GraphType declaringType, string name, bool isStatic, bool isBoundary, GraphType propertyType, bool isList, Attribute[] attributes)
		{
			declaringType.AddProperty(new GraphReferenceProperty(declaringType, name, declaringType.Properties.Count, isStatic, isBoundary, propertyType, isList, attributes));
		}

		/// <summary>
		/// Adds a property to the specified <see cref="GraphType"/> that represents an
		/// strongly-typed value value with the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="declaringType">The <see cref="GraphType"/> the property is for</param>
		/// <param name="name">The name of the property</param>
		/// <param name="propertyType">The <see cref="Type"/> of the property</param>
		/// <param name="attributes">The attributes assigned to the property</param>
		protected virtual void AddProperty(GraphType declaringType, string name, bool isStatic, Type propertyType, Attribute[] attributes)
		{
			declaringType.AddProperty(new GraphValueProperty(declaringType, name, declaringType.Properties.Count, isStatic, propertyType, attributes));
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
