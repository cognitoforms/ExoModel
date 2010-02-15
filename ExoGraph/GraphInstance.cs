using System.Collections.Generic;
using System.Collections;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System;
using System.ComponentModel;

namespace ExoGraph
{
	/// <summary>
	/// Represents an instance of a <see cref="GraphType"/> in a physical graph.
	/// </summary>
	[DataContract]
	[Serializable]
	public class GraphInstance
	{
		#region Fields

		string id;
		object instance;
		GraphType type;

		Dictionary<GraphReferenceProperty, ReferenceSet> outReferences =
			new Dictionary<GraphReferenceProperty, ReferenceSet>();

		Dictionary<GraphReferenceProperty, ReferenceSet> inReferences =
			new Dictionary<GraphReferenceProperty, ReferenceSet>();

		[NonSerialized]
		object extension;

		bool[] hasBeenAccessed;

		bool isInitialized;

		static GraphReference[] noReferences = new GraphReference[0];

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="GraphInstance"/> for the specified <see cref="GraphType"/>
		/// and actual graph object instance.
		/// </summary>
		/// <param name="graphType"></param>
		/// <param name="instance"></param>
		internal GraphInstance(GraphType type, object instance)
		{
			this.type = type;
			this.instance = instance;
			this.hasBeenAccessed = new bool[type.Properties.Count];
		}

		/// <summary>
		/// Creates a new <see cref="GraphInstance"/> for the specified <see cref="GraphType"/>
		/// and id, but does not yet represent a real <see cref="GraphInstance"/>.
		/// </summary>
		/// <param name="graphType"></param>
		/// <param name="id"></param>
		internal GraphInstance(GraphType type, string id)
		{
			this.id = id;
			this.type = type;
			this.hasBeenAccessed = new bool[type.Properties.Count];
		}

		#endregion

		#region Properties

		/// <summary>
		/// The <see cref="GraphType"/> of the instance in the graph.
		/// </summary>
		public GraphType Type
		{
			get
			{
				return type;
			}
		}

		[DataMember(Name = "type", Order = 1)]
		string TypeName
		{
			get
			{
				return Type.Name;
			}
			set
			{
				type = GraphContext.Current.GetGraphType(value);
			}
		}

		/// <summary>
		/// Gets the identifier for persisted instances.
		/// </summary>
		[DataMember(Name = "id", Order = 2)]
		public string Id
		{
			get
			{
				if (instance != null)
				{
					if (id == null)
						return this.id = type.Context.GetId(instance) ?? type.Context.GenerateId();
					else
						return Type.Context.GetId(instance) ?? id;
				}
				return id;
			}
			internal set
			{
				id = value;
			}
		}

		/// <summary>
		/// Indicates whether the instance is new or has been persisted.
		/// </summary>
		public bool IsNew
		{
			get
			{
				return Type.Context.GetId(instance) == null;
			}
		}

		/// <summary>
		/// The actual graph object instance.
		/// </summary>
		public object Instance
		{
			get
			{
				return instance;
			}
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
				return Type.Properties[property].GetValue(instance);
			}
			set
			{
				Type.Properties[property].SetValue(instance, value);
			}
		}

		#endregion

		#region Methods

		public TExtension GetExtension<TExtension>()
			where TExtension : class
		{
			if (extension == null)
				this.extension = Type.CreateExtension(this);
			return extension as TExtension;
		}

		internal IEnumerable<GraphReference> GetInReferences(GraphReferenceProperty property)
		{
			ReferenceSet references;
			return inReferences.TryGetValue(property, out references) ? (IEnumerable<GraphReference>)references : (IEnumerable<GraphReference>)noReferences;
		}

		internal IEnumerable<GraphReference> GetOutReferences(GraphReferenceProperty property)
		{
			ReferenceSet references;
			return outReferences.TryGetValue(property, out references) ? (IEnumerable<GraphReference>)references : (IEnumerable<GraphReference>)noReferences;
		}

		/// <summary>
		/// Gets the <see cref="GraphReference"/> established for the specified 
		/// <see cref="GraphReferenceProperty"/> and <see cref="GraphInstance"/>.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="instance">The child instance associated with the property</param>
		/// <returns>The requested reference, if established, otherwise null</returns>
		internal GraphReference GetOutReference(GraphReferenceProperty property, GraphInstance instance)
		{
			// Return null if there are no references established for the property
			ReferenceSet references;
			if (!outReferences.TryGetValue(property, out references))
				return null;

			// Otherwise, look up the reference in the set based on the specified instance
			return references[instance];
		}

		/// <summary>
		/// Adds a reference for the specified instance and property.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="instance">The instance the reference is for</param>
		/// <param name="isLoading">
		/// Indicates whether the reference is being establish while the graph is loading
		/// or represents a real change
		/// </param>
		internal void AddReference(GraphReferenceProperty property, GraphInstance instance, bool isLoading)
		{
			// Create and add this reference to the parent and child instances
			GraphReference reference = new GraphReference(property, this, instance);

			// Create a reference set if no out references have been established for this property
			ReferenceSet references;
			if (!outReferences.TryGetValue(property, out references))
			{
				references = new ReferenceSet(ReferenceDirection.Out);
				outReferences.Add(property, references);
			}

			// Add the out reference
			references.Add(reference);

			// Only add in references if the property is not a boundary between separately scoped instances
			if (!property.IsBoundary)
			{
				// Create a reference set if no in references have been established for this property
				if (!instance.inReferences.TryGetValue(property, out references))
				{
					references = new ReferenceSet(ReferenceDirection.In);
					instance.inReferences.Add(property, references);
				}

				// Add the in reference
				references.Add(reference);
			}

			// Notify the reference property that this reference has been established if not currently loading
			if (!isLoading)
				property.OnChange(this);
		}

		/// <summary>
		/// Removes the specified reference.
		/// </summary>
		/// <param name="reference">The reference to remove</param>
		internal void RemoveReference(GraphReference reference)
		{
			// Exit immediately if the reference is null
			if (reference == null)
				return;

			ReferenceSet references;

			// Remove the out reference
			if (outReferences.TryGetValue(reference.Property, out references))
				references.Remove(reference);

			// Remove the in reference
			if (reference.Out.inReferences.TryGetValue(reference.Property, out references))
				references.Remove(reference);

			// Notify the reference property that this reference has been removed
			reference.Property.OnChange(this);
		}

		/// <summary>
		/// Raises that the initialization event is raised for the instance when it is first used.
		/// </summary>
		internal void OnAccess()
		{
			if (!isInitialized)
			{
				isInitialized = true;
				if (IsNew)
					new GraphInitEvent.InitNew(this).Notify();
				else
					new GraphInitEvent.InitExisting(this).Notify();
			}
		}

		/// <summary>
		/// Performs special initialization of the graph when a property is first accessed.
		/// </summary>
		/// <param name="property"></param>
		internal void OnFirstAccess(GraphProperty property)
		{
			// If the property is a reference property, establish edges
			if (property is GraphReferenceProperty)
			{
				// Prevent gets on reference properties from raising get notifications
				using (Type.Context.SuspendGetNotifications())
				{
					GraphReferenceProperty refProp = (GraphReferenceProperty)property;
					if (refProp.IsList)
					{
						// Add references for all of the items in the list
						foreach (GraphInstance reference in GetList(refProp))
							AddReference(refProp, reference, true);

						// Allow the context to subscribe to list change notifications
						IList list = Type.Context.ConvertToList(refProp, this[property.Name]);
						if (list != null)
							Type.Context.OnStartTrackingList(this, (GraphReferenceProperty)property, list);
					}
					else
					{
						// Add a reference if the property is not null
						GraphInstance reference = GetReference(refProp);
						if (reference != null)
							AddReference(refProp, reference, true);
					}
				}
			}

			// Mark the property as having been accessed
			hasBeenAccessed[property.Index] = true;


		}

		/// <summary>
		/// Indicates whether the property has been accessed on this instance.
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		internal bool HasBeenAccessed(GraphProperty property)
		{
			return hasBeenAccessed[property.Index];
		}

		/// <summary>
		/// Gets the <see cref="GraphInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		public GraphInstance GetReference(string property)
		{
			return GetReference(Type.OutReferences[property]);
		}

		/// <summary>
		/// Gets the <see cref="GraphInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphReferenceProperty"/></param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		public GraphInstance GetReference(GraphReferenceProperty property)
		{
			object reference = property.GetValue(instance);
			if (reference != null)
				return Type.Context.GetGraphInstance(reference);
			return null;
		}

		/// <summary>
		/// Gets the value assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The value of the property</returns>
		public object GetValue(string property)
		{
			return GetValue(Type.Values[property]);
		}

		/// <summary>
		/// Gets the value assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphValueProperty"/></param>
		/// <returns>The value of the property</returns>
		public object GetValue(GraphValueProperty property)
		{
			return property.GetValue(instance);
		}

		/// <summary>
		/// Gets the list of <see cref="GraphInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of property</param>
		/// <returns>The list of instances</returns>
		public GraphInstanceList GetList(string property)
		{
			return GetList(type.OutReferences[property]);
		}

		/// <summary>
		/// Gets the list of <see cref="GraphInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphReferenceProperty"/></param>
		/// <returns>The list of instances</returns>
		public GraphInstanceList GetList(GraphReferenceProperty property)
		{
			return new GraphInstanceList(this, property);
		}

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		public void SetReference(string property, GraphInstance value)
		{
			SetReference(Type.OutReferences[property], value);
		}

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		public void SetReference(GraphReferenceProperty property, GraphInstance value)
		{
			property.SetValue(instance, value == null ? null : value.Instance);
		}

		/// <summary>
		/// Sets a property to the specified value.
		/// </summary>
		/// <param name="property">The property to set</param>
		/// <param name="value">The value of the property</param>
		public void SetValue(string property, object value)
		{
			SetValue(Type.Values[property], value);
		}

		/// <summary>
		/// Sets a property to the specified value.
		/// </summary>
		/// <param name="property">The property to set</param>
		/// <param name="value">The value of the property</param>
		public void SetValue(GraphValueProperty property, object value)
		{
			property.SetValue(instance, value);
		}

		/// <summary>
		/// Saves changes to the current <see cref="GraphInstance"/> and all related 
		/// instances in the graph.
		/// </summary>
		public void Save()
		{
			Type.Context.Save(this);
		}

		/// <summary>
		/// Deletes the current <see cref="GraphInstance"/>.
		/// </summary>
		public void Delete()
		{
			Type.Context.DeleteInstance(this.Instance);
		}

		/// <summary>
		/// Raises a custom event for the current instance.
		/// </summary>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="customEvent"></param>
		public void RaiseEvent<TEvent>(TEvent customEvent)
		{
			// Create a new graph domain event and notify subscribers
			new GraphCustomEvent<TEvent>(this, customEvent).Notify();
		}

		/// <summary>
		/// Returns the string representation of the underlying graph instance.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return "" + instance;
		}

		#endregion

		#region ReferenceSet

		/// <summary>
		/// Represents a list of in or out references maintained by a <see cref="GraphInstance"/>.
		/// </summary>
		/// <remarks>
		/// For performance reasons, the set is optimized to store single <see cref="GraphReference"/> instances
		/// without having to create a dictionary.
		/// </remarks>
		[Serializable]
		class ReferenceSet : IEnumerable<GraphReference>
		{
			ReferenceDirection direction;
			GraphReference reference;
			Dictionary<GraphInstance, GraphReference> referenceList;

			/// <summary>
			/// Creates a new <see cref="ReferenceSet"/> with the specified reference direction.
			/// </summary>
			/// <param name="direction"></param>
			internal ReferenceSet(ReferenceDirection direction)
			{
				this.direction = direction;
			}

			/// <summary>
			/// Gets the <see cref="GraphReference"/> that corresponds to the specified <see cref="GraphInstance"/>.
			/// </summary>
			/// <param name="instance"></param>
			/// <returns></returns>
			internal GraphReference this[GraphInstance instance]
			{
				get
				{
					if (this.referenceList != null)
					{
						GraphReference reference;
						this.referenceList.TryGetValue(instance, out reference);
						return reference;
					}
					else if (this.reference != null && instance == (direction == ReferenceDirection.In ? this.reference.In : this.reference.Out))
						return this.reference;
					return null;
				}
			}

			/// <summary>
			/// Adds the <see cref="GraphReference"/> to the current set.
			/// </summary>
			/// <param name="reference"></param>
			internal void Add(GraphReference reference)
			{
				if (referenceList != null)
					referenceList.Add(direction == ReferenceDirection.In ? reference.In : reference.Out, reference);
				else if (this.reference == null)
					this.reference = reference;
				else
				{
					referenceList = new Dictionary<GraphInstance, GraphReference>();
					referenceList.Add(direction == ReferenceDirection.In ? reference.In : reference.Out, reference);
					referenceList.Add(direction == ReferenceDirection.In ? this.reference.In : this.reference.Out, this.reference);
				}
			}

			/// <summary>
			/// Removes the <see cref="GraphReference"/> from the current set.
			/// </summary>
			/// <param name="reference"></param>
			internal void Remove(GraphReference reference)
			{
				if (referenceList != null)
					referenceList.Remove(direction == ReferenceDirection.In ? reference.In : reference.Out);
				else if (this.reference == reference)
					this.reference = null;
			}

			/// <summary>
			/// Enumerates over the <see cref="GraphReference"/> instances in the set.
			/// </summary>
			/// <returns></returns>
			public IEnumerator<GraphReference> GetEnumerator()
			{
				if (referenceList != null)
				{
					foreach (GraphReference r in referenceList.Values)
						yield return r;
				}
				else if (reference != null)
					yield return reference;
			}

			/// <summary>
			/// Enumerates over the <see cref="GraphReference"/> instances in the set.
			/// </summary>
			/// <returns></returns>
			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		/// <summary>
		/// Specifies the direction of a <see cref="ReferenceSet"/>.
		/// </summary>
		enum ReferenceDirection
		{
			In,
			Out
		}

		#endregion
	}
}
