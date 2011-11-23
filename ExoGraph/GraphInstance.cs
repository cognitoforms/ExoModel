﻿using System.Collections.Generic;
using System.Collections;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Linq;

namespace ExoGraph
{
	/// <summary>
	/// Represents an instance of a <see cref="GraphType"/> in a physical graph.
	/// </summary>
	[DataContract]
	[Serializable]
	public class GraphInstance : IGraphPropertySource
	{
		#region Fields

		string id;
		object instance;
		GraphType type;
		string typeName;

		[NonSerialized]
		Dictionary<GraphReferenceProperty, ReferenceSet> outReferences =
			new Dictionary<GraphReferenceProperty, ReferenceSet>();

		[NonSerialized]
		Dictionary<GraphReferenceProperty, ReferenceSet> inReferences =
			new Dictionary<GraphReferenceProperty, ReferenceSet>();

		[NonSerialized]
		object extension;

		BitArray hasBeenAccessed;
		BitArray isBeingAccessed;
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
		public GraphInstance(object instance)
		{
			this.type = GraphType.Unknown;
			this.instance = instance;
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
			this.hasBeenAccessed = new BitArray(type.Properties.Count);
			this.isBeingAccessed = new BitArray(type.Properties.Count);
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
				if (!isInitialized && type == GraphType.Unknown)
					OnAccess();
				return type ?? GraphContext.Current.GetGraphType(typeName);
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
					{
						// Ensure that calling OnAccess does not assign an id
						if (!isInitialized)
							OnAccess();

						// Return id assigned by first access (OnAccess), or assign on as needed
						return this.id ?? (this.id = Type.GetId(instance) ?? Type.Context.GenerateId());
					}
					else
						return Type.GetId(instance) ?? id;
				}
				return id;
			}
			internal set
			{
				id = value;
			}
		}

		/// <summary>
		/// Gets the original id of the instance, which may be different from the
		/// current id if the instance has transitioned from new to existing.
		/// </summary>
		public string OriginalId
		{
			get
			{
				return id ?? Id;
			}
		}

		/// <summary>
		/// Indicates whether the instance is new or has been persisted.
		/// </summary>
		public bool IsNew
		{
			get
			{
				return Type.GetId(instance) == null;
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

		Dictionary<GraphReferenceProperty, ReferenceSet> OutReferences
		{
			get
			{
				if (outReferences == null)
				{
					if (IsCached)
						return new Dictionary<GraphReferenceProperty, ReferenceSet>();
					else
						outReferences = new Dictionary<GraphReferenceProperty, ReferenceSet>();
				}

				return outReferences;
			}
		}

		Dictionary<GraphReferenceProperty, ReferenceSet> InReferences
		{
			get
			{
				if(inReferences == null)
				{
					if (IsCached)
						return new Dictionary<GraphReferenceProperty, ReferenceSet>();
					else
						inReferences = new Dictionary<GraphReferenceProperty, ReferenceSet>();
				}

				return inReferences;
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
				return this[Type.Properties[property]];
			}
			set
			{
				this[Type.Properties[property]] = value;
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
				return property is GraphValueProperty ? GetValue((GraphValueProperty)property) : property.GetValue(instance);
			}
			set
			{
				if (property is GraphValueProperty)
					SetValue((GraphValueProperty)property, value);
				else
					property.SetValue(instance, value);
			}
		}

		/// <summary>
		/// Explicit implementation of <see cref="IGraphPropertySource"/> exposing the set of properties for the current instance.
		/// </summary>
		GraphPropertyList IGraphPropertySource.Properties
		{
			get
			{
				return Type.Properties;
			}
		}

		/// <summary>
		/// True if this object is shared across threads
		/// </summary>
		public bool IsCached { get; private set; }

		/// <summary>
		/// Gets an object that be used by Monitor to synchronize multi-threaded access
		/// </summary>
		public IDisposable Lock()
		{
			return Type.GetLock(Instance);
		}
		#endregion

		#region Methods

		/// <summary>
		/// Gets or creates an extension of the specified type that will be associated with the
		/// current <see cref="GraphInstance"/>.  Once created, the extension will continue to be
		/// associated with the instance and cannot be replaced.
		/// </summary>
		/// <typeparam name="TExtension"></typeparam>
		/// <returns></returns>
		public TExtension GetExtension<TExtension>()
			where TExtension : class, new()
		{
			// First check if the extension associated with the instance is ready to return
			if (extension is TExtension)
				return (TExtension)extension;

			// Then see if it needs to be initialized
			if (extension == null)
				return (TExtension)(extension = new TExtension());

			// Next, see if the extension is list of extensions
			ListDictionary extensions = extension as ListDictionary;
			
			// If not, create a list and store the current extension in it
			if (extensions == null)
			{
				extensions = new ListDictionary();
				extensions.Add(extension.GetType(), extension);
				extension = extensions;
			}

			// Otherwise, Check to see if the extension is already in the list
			else
			{
				TExtension exl = (TExtension)extensions[typeof(TExtension)];
				if (exl != null)
					return exl;
			}

			// Finally, create the requested extension and add it to the extension list
			TExtension exn = new TExtension();
			extensions.Add(typeof(TExtension), exn);
			return exn;
		}

		public void OnPropertyGet(string property)
		{
			var p = Type.Properties[property];
			if (p != null)
				OnPropertyGet(p);
		}

		public void OnPropertyGet(GraphProperty property)
		{
			// Static notifications are not supported
			if (property.IsStatic)
				return;

			// Raise the property get event
			new GraphPropertyGetEvent(this, property).Notify();
		}

		public void OnPropertySet(string property, object oldValue, object newValue)
		{
			var p = Type.Properties[property];
			if (p == null)
				return;
			if ((oldValue == null ^ newValue == null) || (oldValue != null && !oldValue.Equals(newValue)))
				OnPropertyChanged(p, oldValue, newValue);
		}

		public void OnPropertyChanged(string property, object oldValue, object newValue)
		{
			var p = Type.Properties[property];
			if (p != null)
				OnPropertyChanged(p, oldValue, newValue);
		}

		public void OnPropertyChanged(GraphProperty property, object oldValue, object newValue)
		{
			Type.OnPropertyChanged(this, property, oldValue, newValue);
		}

		internal IEnumerable<GraphReference> GetInReferences(GraphReferenceProperty property)
		{
			ReferenceSet references;
			return InReferences.TryGetValue(property, out references) ? (IEnumerable<GraphReference>)references : (IEnumerable<GraphReference>)noReferences;
		}

		internal IEnumerable<GraphReference> GetOutReferences(GraphReferenceProperty property)
		{
			ReferenceSet references;
			return OutReferences.TryGetValue(property, out references) ? (IEnumerable<GraphReference>)references : (IEnumerable<GraphReference>)noReferences;
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
			if (!OutReferences.TryGetValue(property, out references))
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
			if (!OutReferences.TryGetValue(property, out references))
			{
				references = new ReferenceSet(ReferenceDirection.Out);
				OutReferences.Add(property, references);
			}

			// Add the out reference
			references.Add(reference);

			// Only add in references if the property is not a boundary between separately scoped instances
			if (Type.Scope == instance.Type.Scope)
			{
				// Create a reference set if no in references have been established for this property
				if (!instance.InReferences.TryGetValue(property, out references))
				{
					references = new ReferenceSet(ReferenceDirection.In);
					instance.InReferences.Add(property, references);
				}

				// Add the in reference
				references.Add(reference);
			}

			// Notify the reference property that this reference has been established if not currently loading
			if (!isLoading)
				property.NotifyPathChange(this);
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
			if (OutReferences.TryGetValue(reference.Property, out references))
				references.Remove(reference);

			// Remove the in reference
			if (reference.Out.InReferences.TryGetValue(reference.Property, out references))
				references.Remove(reference);

			// Notify the reference property that this reference has been removed
			reference.Property.NotifyPathChange(this);
		}

		/// <summary>
		/// Performs initialization and raises the init event for an instance when it is first used.
		/// </summary>
		internal void OnAccess()
		{
			// Perform initialization if this is the first time the instance has been accessed
			if (!isInitialized)
			{
				// Mark the instance as initialized to avoid recursive initialization
				isInitialized = true;

				// Initialize the graph type if necessary
				if (type == GraphType.Unknown)
				{
					GraphType knownType = GraphContext.Current.GetGraphType(instance);
					hasBeenAccessed = new BitArray(knownType.PropertyCount);
					isBeingAccessed = new BitArray(knownType.PropertyCount);
					IsCached = knownType.IsCached(instance);
					if (IsCached)
					{
						inReferences = null;
						outReferences = null;
						typeName = knownType.Name;
						type = null;
					}
					else
						type = knownType;
				}

				// Raise the appropriate init event
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
			if (HasBeenAccessed(property)) return;

			// If the property is a reference property, establish edges
			if (property is GraphReferenceProperty)
			{
				GraphReferenceProperty refProp = (GraphReferenceProperty)property;
				if (refProp.IsList)
				{
					// Add references for all of the items in the list
					foreach (GraphInstance reference in GetList(refProp))
						AddReference(refProp, reference, true);

					// Allow the context to subscribe to list change notifications
					IList list = Type.ConvertToList(refProp, this[property.Name]);
					if (list != null && !(refProp.PropertyType.IsCached(list)))
						Type.OnStartTrackingList(this, (GraphReferenceProperty)property, list);
				}
				else
				{
					// Add a reference if the property is not null
					GraphInstance reference = GetReference(refProp);
					if (reference != null)
						AddReference(refProp, reference, true);
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
		public bool HasBeenAccessed(GraphProperty property)
		{
			return hasBeenAccessed[property.Index];
		}

		/// <summary>
		/// Notify that a property is being accessed for the first time.
		/// </summary>
		/// <param name="property"></param>
		internal void SetIsPropertyBeingAccessed(GraphProperty property, bool value)
		{
			isBeingAccessed[property.Index] = value;
		}

		/// <summary>
		/// Indicates that the given property is being accessed for the first time on the given graph instance,
		/// so property get events should be temporarily suspended.
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		internal bool IsPropertyBeingAccessed(GraphProperty property)
		{
			return isBeingAccessed[property.Index];
		}

		/// <summary>
		/// Returns a cloner for copying a graph rooted at this <see cref="GraphInstance"/>
		/// </summary>
		/// <param name="paths">Represents a set of graph paths for which new instances will be created. Properties not
		/// included in these paths will be copied by reference.</param>
		/// <returns></returns>
		public Cloner Clone(params string[] paths)
		{
			return new Cloner(this, paths);
		}

		/// <summary>
		/// Returns a cloner for copying a graph rooted at this <see cref="GraphInstance"/>
		/// </summary>
		/// <param name="paths">Represents a set of graph paths for which new instances will be created. Properties not
		/// included in these paths will be copied by reference.</param>
		/// <returns></returns>
		public Cloner Clone(IEnumerable<string> paths)
		{
			return new Cloner(this, paths);
		}

		/// <summary>
		/// Returns a cloner for copying a graph rooted at this <see cref="GraphInstance"/>, accepting
		/// an existing copy of this <see cref="GraphInstance"/>
		/// </summary>
		/// <param name="destination">A pre-existing copy of this <see cref="GraphInstance"/></param>
		/// <param name="paths">Represents a set of graph paths for which new instances will be created. Properties not
		/// included in these paths will be copied by reference.</param>
		/// <returns></returns>
		public Cloner CloneInto(GraphInstance destination, params string[] paths)
		{
			return new Cloner(this, destination, paths);
		}

		/// <summary>
		/// Copies the property values of the current instance to the specified clone instance,
		/// using the mapping to look up the correct instance to include for list and reference properties.
		/// </summary>
		/// <param name="clone"></param>
		/// <param name="mapping"></param>
		void CloneProperties(GraphInstance clone, IDictionary<GraphInstance, GraphInstance> mapping, List<Cloner.FilterInfo> filters, Dictionary<Type, object> overrides)
		{
			// Copy all property data for read-write properties
			foreach (var property in Type.Properties.Where(p => !p.IsReadOnly && !p.IsStatic))
			{
				// Value
				if (property is GraphValueProperty)
					clone.SetValue((GraphValueProperty)property, GetValue((GraphValueProperty)property));
				else
				{
					GraphInstance cloneInstance;
					var reference = (GraphReferenceProperty)property;
					
					// List
					if (reference.IsList)
					{
						var toList = clone.GetList(reference);
						foreach (var instance in GetList(reference))
							if(filters.All(f => f.Allows(property, this.Instance, instance.Instance)))
								toList.Add(mapping.TryGetValue(instance, out cloneInstance) ? cloneInstance : instance);
					}

					// Reference
					else
					{
						var instance = GetReference(reference);
						if (instance != null && filters.All(f => f.Allows(property, this.Instance, instance.Instance)))
							clone.SetReference(reference, mapping.TryGetValue(instance, out cloneInstance) ? cloneInstance : instance);
						else
							clone.SetReference(reference, null);
					}
				}
			}

			// Determine if there is a clone override to perform
			foreach (Type type in overrides.Keys)
			{
				if (type.IsInstanceOfType(clone.Instance))
					typeof(Action<,>).MakeGenericType(type, type).GetMethod("Invoke").Invoke(overrides[type], new object[] { this.Instance, clone.Instance });
			}
		}

		/// <summary>
		/// Recursively clones the current instance based on the specified graph steps,
		/// storing the clones in the specified mapping dictionary.
		/// </summary>
		/// <param name="steps"></param>
		/// <param name="mapping"></param>
		void CloneInstance(GraphStepList steps, IDictionary<GraphInstance, GraphInstance> mapping, List<Cloner.FilterInfo> filters, List<Cloner.WhereInfo> wheres)
		{
			// if instance has not been cloned, clone it
			if (!mapping.ContainsKey(this))
			{
				// Create the new clone instance
				GraphInstance clone = Type.Create();
				mapping.Add(this, clone);
			}

		    // Recursively clone child instances
			foreach (var step in steps)
				foreach (var instance in step.GetInstances(this))
				{
					if (filters.All(f => f.Allows(step.Property, this, instance)) && wheres.All(w => w.Allows(step, this, instance)))
						instance.CloneInstance(step.NextSteps, mapping, filters, wheres);
				}
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
				return Type.GetGraphInstance(reference);
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
			if (property.AutoConvert)
				return property.Converter.ConvertTo(property.GetValue(instance), typeof(object));
			else
				return property.GetValue(instance);
		}

		/// <summary>
		/// Gets the list of <see cref="GraphInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of property</param>
		/// <returns>The list of instances</returns>
		public GraphInstanceList GetList(string property)
		{
			return GetList(Type.OutReferences[property]);
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
			if (property.AutoConvert && (value == null || property.Converter.CanConvertFrom(value.GetType())) )
				property.SetValue(instance, property.Converter.ConvertFrom(value));
			else
				property.SetValue(instance, value);
		}

		/// <summary>
		/// Saves changes to the current <see cref="GraphInstance"/> and all related 
		/// instances in the graph.
		/// </summary>
		public void Save()
		{
			Type.SaveInstance(this);
		}

		/// <summary>
		/// Deletes the current <see cref="GraphInstance"/>.
		/// </summary>
		public void Delete()
		{
			Type.DeleteInstance(this);
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
				{
					// be sure not to double add references
					referenceList[direction == ReferenceDirection.In ? reference.In : reference.Out] = reference;
				}
				else if (this.reference == null)
					this.reference = reference;
				else
				{
					referenceList = new Dictionary<GraphInstance, GraphReference>();
					referenceList[direction == ReferenceDirection.In ? reference.In : reference.Out] = reference;

					// be sure not to double add references
					referenceList[direction == ReferenceDirection.In ? this.reference.In : this.reference.Out] = this.reference;
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

		#region Cloner

		public class Cloner
		{
			GraphInstance instance;
			GraphInstance destination;
			List<GraphPath> paths = new List<GraphPath>();
			Dictionary<Type, object> overrides = new Dictionary<Type, object>();
			List<FilterInfo> filters = new List<FilterInfo>();
			List<WhereInfo> wheres = new List<WhereInfo>();
			Dictionary<GraphInstance, GraphInstance> maps = new Dictionary<GraphInstance,GraphInstance>();

			internal Cloner(GraphInstance instance, IEnumerable<string> paths)
				: this(instance, null, paths)
			{
			}

			internal Cloner(GraphInstance instance, GraphInstance destination, IEnumerable<string> paths)
			{
				this.instance = instance;
				this.destination = destination;
				this.paths.AddRange(paths.Select(p => instance.Type.GetPath(p)));
			}

			/// <summary>
			/// Includes additional paths used to instantiate new objects
			/// </summary>
			public Cloner Clone(params string[] paths)
			{
				return Clone(paths.AsEnumerable<string>());
			}

			/// <summary>
			/// Includes additional paths used to instantiate new objects
			/// </summary>
			public Cloner Clone(IEnumerable<string> paths)
			{
				this.paths.AddRange(paths.Select(p => instance.Type.GetPath(p)));
				return this;
			}

			/// <summary>
			/// Allows for additional operations to be performed on a copied
			/// object of type <typeparam name="TType" />.
			/// <param name="fixup" /> will be executed after all properties
			/// have been populated on the copy.
			/// </summary>
			public Cloner Override<TType>(Action<TType, TType> fixup)
			{
				overrides.Add(typeof(TType), fixup);
				return this;
			}

			/// <summary>
			/// Conditionally determine whether to instantiate a new object
			/// when copying a <typeparamref name="TValue"/>, given the specific
			/// <see cref="GraphStep"/>, original instance (<typeparamref name="TType"/>),
			/// and original value (<typeparamref name="TValue"/>) of the 
			/// property or list member.
			/// </summary>
			/// <typeparam name="TType">Declaring type of property to copy</typeparam>
			/// <typeparam name="TValue">Property type of property to copy</typeparam>
			/// <param name="where">returns true when a new instance of <typeparamref name="TValue"/>
			/// should be instantiated.</param>
			public Cloner Where<TType, TValue>(Func<GraphStep, TType, TValue, bool> where)
			{
				wheres.Add(new WhereInfo<TType, TValue> { When = where });
				return this;
			}

			/// <summary>
			/// Conditionally determine whether to copy a value, given the specific
			/// <see cref="GraphProperty"/>, original instance (<typeparamref name="TType"/>),
			/// and original value (<typeparamref name="TValue"/>) of the 
			/// property or list member.  Filtered values will neither be used to instantiate new objects
			/// nor be copied as references on the new graph.
			/// </summary>
			/// <typeparam name="TType">Declaring type of property to copy</typeparam>
			/// <typeparam name="TValue">Property type of property to copy</typeparam>
			/// <param name="filter">returns true when the value of <typeparamref name="TValue"/>
			/// should be considered for the cloning process.</param>
			/// <returns></returns>
			public Cloner Filter<TType, TValue>(Func<GraphProperty, TType, TValue, bool> filter)
			{
				filters.Add(new FilterInfo<TType, TValue> { When = filter });
				return this;
			}

			/// <summary>
			/// Adds a mapping between a source <see cref="GraphInstance"/> and a destination
			/// <see cref="GraphInstance"/>.
			/// 
			/// If paths provided to the cloner cause <paramref name="from"/> to have been cloned then
			/// the cloned <see cref="GraphInstance"/> will be copied, and not the <paramref name="to"/>
			/// that may have been provided here.
			/// </summary>
			/// <param name="from">A <see cref="GraphInstance"/> in the source graph.</param>
			/// <param name="to">The <see cref="GraphInstance"/> that will be copied instead.</param>
			/// <returns></returns>
			public Cloner Map(GraphInstance from, GraphInstance to)
			{
				maps.Add(from, to);
				return this;
			}

			/// <summary>
			/// Begins the cloning process and returns the copied <see cref="GraphInstance"/>
			/// of the original <see cref="GraphInstance"/>
			/// </summary>
			/// <returns></returns>
			public GraphInstance Invoke()
			{
				GraphInstance result = null;
				GraphEventScope.Perform(() =>
				{
					// Clone instances
					var clones = new Dictionary<GraphInstance, GraphInstance>();

					if (destination != null)
						clones.Add(instance, destination);

					paths.ForEach(p => instance.CloneInstance(p.FirstSteps, clones, filters, wheres));

					var mapUnion = clones.Union(maps.Where(m => !clones.Keys.Contains(m.Key))).ToDictionary(p => p.Key, p => p.Value);

					// Clone properties
					foreach (var clone in clones)
						clone.Key.CloneProperties(clone.Value, mapUnion, filters, overrides);

					// Return the root cloned instance
					result = clones[instance];
				});
				return result;
			}

			internal abstract class WhereInfo
			{
				internal abstract bool Allows(GraphStep step, object item, object value);
			}

			class WhereInfo<TType, TValue> : WhereInfo
			{
				internal Func<GraphStep, TType, TValue, bool> When { get; set; }

				internal override bool Allows(GraphStep step, object instance, object value)
				{
					if (instance is TType && value is TValue)
						return When(step, (TType)instance, (TValue)value);

					// where doesn't even apply
					return true;
				}
			}

			internal abstract class FilterInfo
			{
				internal abstract bool Allows(GraphProperty property, object item, object value);
			}

			class FilterInfo<TType, TValue> : FilterInfo
			{
				internal Func<GraphProperty, TType, TValue, bool> When { get; set; }

				internal override bool Allows(GraphProperty property, object instance, object value)
				{
					if (instance is TType && value is TValue)
						return When(property, (TType)instance, (TValue)value);

					// filter doesn't even apply
					return true;
				}
			}
		}

		#endregion
	}
}
