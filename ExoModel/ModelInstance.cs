using System.Collections.Generic;
using System.Collections;
using System.Runtime.Serialization;
using System.Xml;
using System;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExoModel
{
	/// <summary>
	/// Represents an instance of a <see cref="ModelType"/> in a physical model.
	/// </summary>
	[Serializable]
	public class ModelInstance : IModelPropertySource, IFormattable
	{
		#region Fields

		string id;
		object instance;
		ModelType type;
		string typeName;

		[NonSerialized]
		Dictionary<ModelReferenceProperty, ReferenceSet> outReferences =
			new Dictionary<ModelReferenceProperty, ReferenceSet>();

		[NonSerialized]
		Dictionary<ModelReferenceProperty, ReferenceSet> inReferences =
			new Dictionary<ModelReferenceProperty, ReferenceSet>();

		[NonSerialized]
		object extension;

		BitArray hasBeenAccessed;
		BitArray isBeingAccessed;
		bool isInitialized;

		static ModelReference[] noReferences = new ModelReference[0];

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="ModelInstance"/> for the specified <see cref="ModelType"/>
		/// and actual model object instance.
		/// </summary>
		/// <param name="modelType"></param>
		/// <param name="instance"></param>
		public ModelInstance(object instance)
		{
			this.type = ModelType.Unknown;
			this.instance = instance;
		}

		/// <summary>
		/// Creates a new <see cref="ModelInstance"/> for the specified <see cref="ModelType"/>
		/// and id, but does not yet represent a real <see cref="ModelInstance"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="id"></param>
		protected internal ModelInstance(ModelType type, string id)
		{
			this.id = id;
			this.type = type;
			this.hasBeenAccessed = new BitArray(type.Properties.Count);
			this.isBeingAccessed = new BitArray(type.Properties.Count);
			
			// Assume subclasses implementing IModelInstance are the underlying instance
			if (this is IModelInstance)
				this.instance = this;
		}

		#endregion

		#region Properties
		
		/// <summary>
		/// The <see cref="ModelType"/> of the instance in the model.
		/// </summary>
		public ModelType Type
		{
			get
			{
				if (!isInitialized && type == ModelType.Unknown)
					OnAccess();
				return type ?? ModelContext.Current.GetModelType(typeName);
			}
			set
			{
				if (!isInitialized && type == ModelType.Unknown)
				{
					type = value;
					OnAccess();
				}
			}

		}

		/// <summary>
		/// Gets the identifier for persisted instances.
		/// </summary>
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
						return this.id ?? (this.id = Type.GetId(instance) ?? Type.Context.GenerateId(IsCached));
					}
					else
						return Type.GetId(instance) ?? id;
				}
				return id;
			}
			protected internal set
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
		/// Indicates whether the instance has pending changes that have not been persisted.
		/// </summary>
		public bool IsModified
		{
			get
			{
				return Type.GetIsModified(instance);
			}
		}

		/// <summary>
		/// Indicates whether the instance has been marked for deletion.
		/// </summary>
		public bool IsPendingDelete
		{
			get
			{
				return Type.GetIsPendingDelete(instance);
			}
			set
			{
				if (IsPendingDelete != value)
					Type.SetIsPendingDelete(instance, value);
			}
		}

		/// <summary>
		/// Indicates whether the instance has been permanently deleted.
		/// </summary>
		public bool IsDeleted
		{
			get
			{
				return Type.GetIsDeleted(instance);
			}
		}

		/// <summary>
		/// The actual model object instance.
		/// </summary>
		public object Instance
		{
			get
			{
				return instance;
			}
		}

		Dictionary<ModelReferenceProperty, ReferenceSet> OutReferences
		{
			get
			{
				if (outReferences == null)
				{
					if (IsCached)
						return new Dictionary<ModelReferenceProperty, ReferenceSet>();
					else
						outReferences = new Dictionary<ModelReferenceProperty, ReferenceSet>();
				}

				return outReferences;
			}
		}

		Dictionary<ModelReferenceProperty, ReferenceSet> InReferences
		{
			get
			{
				if(inReferences == null)
				{
					if (IsCached)
						return new Dictionary<ModelReferenceProperty, ReferenceSet>();
					else
						inReferences = new Dictionary<ModelReferenceProperty, ReferenceSet>();
				}

				return inReferences;
			}
		}

		/// <summary>
		/// Gets or sets the value of the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The underlying value of the property in the physical model</returns>
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
		/// <param name="property">The <see cref="ModelProperty"/> to get or set</param>
		/// <returns>The underlying value of the property in the physical model</returns>
		public object this[ModelProperty property]
		{
			get
			{
				return property is ModelValueProperty ? GetValue((ModelValueProperty)property) : property.GetValue(instance);
			}
			set
			{
				if (property is ModelValueProperty)
					SetValue((ModelValueProperty)property, value);
				else
					property.SetValue(instance, value);
			}
		}

		/// <summary>
		/// Explicit implementation of <see cref="IModelPropertySource"/> exposing the set of properties for the current instance.
		/// </summary>
		ModelPropertyList IModelPropertySource.Properties
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
		/// Attempts to acquire a lock synchronize multi-threaded access
		/// </summary>
		public void EnterLock(out bool acquired)
		{
			Type.EnterLock(Instance, out acquired);
		}

		/// <summary>
		/// Releases a lock issued by <see cref="EnterLock" />
		/// </summary>
		public void ExitLock(bool acquired)
		{
			Type.ExitLock(Instance, acquired);
		}
		#endregion

		#region Methods

		/// <summary>
		/// Gets or creates an extension of the specified type that will be associated with the
		/// current <see cref="ModelInstance"/>.  Once created, the extension will continue to be
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

		public void OnPropertyGet(ModelProperty property)
		{
			// Static notifications are not supported
			if (property.IsStatic)
				return;

			// Raise the property get event
			new ModelPropertyGetEvent(this, property).Notify();
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

		public void OnPropertyChanged(ModelProperty property, object oldValue, object newValue)
		{
			Type.OnPropertyChanged(this, property, oldValue, newValue);
		}

		public void OnPendingDelete()
		{
			Type.OnPendingDelete(this);
		}

		internal IEnumerable<ModelReference> GetInReferences(ModelReferenceProperty property)
		{
			ReferenceSet references;
			return InReferences.TryGetValue(property, out references) ? (IEnumerable<ModelReference>)references : (IEnumerable<ModelReference>)noReferences;
		}

		internal IEnumerable<ModelReference> GetOutReferences(ModelReferenceProperty property)
		{
			ReferenceSet references;
			return OutReferences.TryGetValue(property, out references) ? (IEnumerable<ModelReference>)references : (IEnumerable<ModelReference>)noReferences;
		}

		/// <summary>
		/// Gets the <see cref="ModelReference"/> established for the specified 
		/// <see cref="ModelReferenceProperty"/> and <see cref="ModelInstance"/>.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="instance">The child instance associated with the property</param>
		/// <returns>The requested reference, if established, otherwise null</returns>
		internal ModelReference GetOutReference(ModelReferenceProperty property, ModelInstance instance)
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
		/// Indicates whether the reference is being establish while the model is loading
		/// or represents a real change
		/// </param>
		internal void AddReference(ModelReferenceProperty property, ModelInstance instance, bool isLoading)
		{
			// Create and add this reference to the parent and child instances
			ModelReference reference = new ModelReference(property, this, instance);

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
		internal void RemoveReference(ModelReference reference)
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

				// Initialize the model type if necessary
				if (type == ModelType.Unknown)
					type = ModelContext.Current.GetModelType(instance);

				hasBeenAccessed = new BitArray(type.PropertyCount);
				isBeingAccessed = new BitArray(type.PropertyCount);
				IsCached = type.IsCached(instance);
				if (IsCached)
				{
					inReferences = null;
					outReferences = null;
					typeName = type.Name;
					type = null;
				}
				

				// Raise the appropriate init event
				if (IsNew)
					new ModelInitEvent.InitNew(this).Notify();
				else
					new ModelInitEvent.InitExisting(this).Notify();
			}
		}

		/// <summary>
		/// Performs special initialization of the model when a property is first accessed.
		/// </summary>
		/// <param name="property"></param>
		internal void OnFirstAccess(ModelProperty property)
		{
			if (HasBeenAccessed(property)) return;

			// If the property is a reference property, establish edges
			if (property is ModelReferenceProperty)
			{
				ModelReferenceProperty refProp = (ModelReferenceProperty)property;
				if (refProp.IsList)
				{
					// Add references for all of the items in the list
					foreach (ModelInstance reference in GetList(refProp))
						AddReference(refProp, reference, true);

					// Allow the context to subscribe to list change notifications
					IList list = Type.ConvertToList(refProp, this[property.Name]);
					if (list != null && !(refProp.PropertyType.IsCached(list)))
						Type.OnStartTrackingList(this, (ModelReferenceProperty)property, list);
				}
				else
				{
					// Add a reference if the property is not null
					ModelInstance reference = GetReference(refProp);
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
		public bool HasBeenAccessed(ModelProperty property)
		{
			return hasBeenAccessed[property.Index];
		}

		/// <summary>
		/// Notify that a property is being accessed for the first time.
		/// </summary>
		/// <param name="property"></param>
		internal void SetIsPropertyBeingAccessed(ModelProperty property, bool value)
		{
			isBeingAccessed[property.Index] = value;
		}

		/// <summary>
		/// Indicates that the given property is being accessed for the first time on the given model instance,
		/// so property get events should be temporarily suspended.
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		internal bool IsPropertyBeingAccessed(ModelProperty property)
		{
			return isBeingAccessed[property.Index];
		}

		/// <summary>
		/// Returns a cloner for copying a model rooted at this <see cref="ModelInstance"/>
		/// </summary>
		/// <param name="paths">Represents a set of model paths for which new instances will be created. Properties not
		/// included in these paths will be copied by reference.</param>
		/// <returns></returns>
		public Cloner Clone(params string[] paths)
		{
			return new Cloner(this, paths);
		}

		/// <summary>
		/// Returns a cloner for copying a model rooted at this <see cref="ModelInstance"/>
		/// </summary>
		/// <param name="paths">Represents a set of model paths for which new instances will be created. Properties not
		/// included in these paths will be copied by reference.</param>
		/// <returns></returns>
		public Cloner Clone(IEnumerable<string> paths)
		{
			return new Cloner(this, paths);
		}

		/// <summary>
		/// Returns a cloner for copying a model rooted at this <see cref="ModelInstance"/>, accepting
		/// an existing copy of this <see cref="ModelInstance"/>
		/// </summary>
		/// <param name="destination">A pre-existing copy of this <see cref="ModelInstance"/></param>
		/// <param name="paths">Represents a set of model paths for which new instances will be created. Properties not
		/// included in these paths will be copied by reference.</param>
		/// <returns></returns>
		public Cloner CloneInto(ModelInstance destination, params string[] paths)
		{
			return new Cloner(this, destination, paths);
		}

		/// <summary>
		/// Copies the property values of the current instance to the specified clone instance,
		/// using the mapping to look up the correct instance to include for list and reference properties.
		/// </summary>
		/// <param name="clone"></param>
		/// <param name="mapping"></param>
		void CloneProperties(ModelInstance clone, IDictionary<ModelInstance, ModelInstance> mapping, List<Cloner.FilterInfo> filters, Dictionary<Type, object> overrides)
		{
			// Copy all property data for read-write properties
			foreach (var property in Type.Properties.Where(p => !p.IsReadOnly && !p.IsStatic))
			{
				if (property is ModelValueProperty)
					CloneValueProperty(clone, filters, (ModelValueProperty)property);
				else
				{
					var referenceProperty = (ModelReferenceProperty)property;

					if (referenceProperty.IsList)
						CloneModelInstanceList(clone, mapping, filters, referenceProperty);
					else
						CloneModelInstance(clone, mapping, filters, referenceProperty);
				}
			}

			// Determine if there is a clone override to perform
			foreach (Type type in overrides.Keys)
			{
				if (type.IsInstanceOfType(clone.Instance))
					typeof(Action<,>).MakeGenericType(type, type).GetMethod("Invoke").Invoke(overrides[type], new object[] { this.Instance, clone.Instance });
			}
		}

		private void CloneValueProperty(ModelInstance clone, List<Cloner.FilterInfo> filters, ModelValueProperty property)
		{
			object value = GetValue(property);

			if (!filters.All(f => f.Allows(property, this.Instance, value)))
				return;

			clone.SetValue(property, value);
		}

		private void CloneModelInstanceList(ModelInstance clone, IDictionary<ModelInstance, ModelInstance> mapping, List<Cloner.FilterInfo> filters, ModelReferenceProperty property)
		{
			ModelInstanceList instanceList = GetList(property);

			if (!filters.All(f => f.Allows(property, this.Instance, instanceList.GetList())))
				return;	

			ModelInstance cloneInstance;
			var toList = clone.GetList(property);
			toList.Clear(); //Ensures the list is empty before cloning in case it is auto-populated through another means.

			foreach (var instance in instanceList)
				if (filters.All(f => f.Allows(property, this.Instance, instance.Instance)))
					toList.Add(mapping.TryGetValue(instance, out cloneInstance) ? cloneInstance : instance);
		}
		
		private void CloneModelInstance(ModelInstance clone, IDictionary<ModelInstance, ModelInstance> mapping, List<Cloner.FilterInfo> filters, ModelReferenceProperty property)
		{
			ModelInstance cloneInstance;
			var instance = GetReference(property);

			if (instance != null && filters.All(f => f.Allows(property, this.Instance, instance.Instance)))
				clone.SetReference(property, mapping.TryGetValue(instance, out cloneInstance) ? cloneInstance : instance);
		}
		
		/// <summary>
		/// Recursively clones the current instance based on the specified model tokens,
		/// storing the clones in the specified mapping dictionary.
		/// </summary>
		/// <param name="tokens"></param>
		/// <param name="mapping"></param>
		void CloneInstance(IEnumerable<ModelStep> steps, IDictionary<ModelInstance, ModelInstance> mapping, List<Cloner.FilterInfo> filters, List<Cloner.WhereInfo> wheres, List<Cloner.PathFuncInfo> pathFuncs)
		{
			// if instance has not been cloned, clone it
			if (!mapping.ContainsKey(this))
			{
				// Create the new clone instance
				ModelInstance clone = Type.Create();
				mapping.Add(this, clone);
			}
			
		    // Recursively clone child instances
			foreach (var step in steps)
				foreach (var instance in step.GetInstances(this))
				{
					if (filters.All(f => f.Allows(step.Property, this.Instance, instance.Instance)) && wheres.All(w => w.Allows(step, this.Instance, instance.Instance)))
						instance.CloneInstance(step.NextSteps.Union(pathFuncs.SelectMany(pf => pf.GetPaths(step, instance.Instance)).SelectMany(p => instance.Type.GetPath(p).FirstSteps)),
							mapping, filters, wheres, pathFuncs);
				}
		}

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		public ModelInstance GetReference(string property)
		{
			return GetReference(Type.References[property]);
		}

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelReferenceProperty"/></param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		public ModelInstance GetReference(ModelReferenceProperty property)
		{
			object reference = property.GetValue(instance);
			if (reference != null)
				return property.PropertyType.GetModelInstance(reference);
			return null;
		}

		/// <summary>
		/// Gets the first <see cref="ModelInstance"/> that is the parent of the current instance through the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelReferenceProperty"/> of the parent referencing this instance</param>
		/// <returns>The first parent referencing this instance via the property, or null if a parent reference does not exist</returns>
		public ModelInstance GetParentReference(ModelReferenceProperty property)
		{
			var reference = GetInReferences(property).FirstOrDefault();
			return reference != null ? reference.In : null;
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
		/// <param name="property">The specific <see cref="ModelValueProperty"/></param>
		/// <returns>The value of the property</returns>
		public object GetValue(ModelValueProperty property)
		{
			if (property.AutoConvert)
				return property.Converter.ConvertTo(property.GetValue(instance), typeof(object));
			else
				return property.GetValue(instance);
		}

		/// <summary>
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The formatted of the property</returns>
		public string GetFormattedValue(string property)
		{
			return GetFormattedValue(Type.Properties[property], null);
		}

		/// <summary>
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelProperty"/></param>
		/// <returns>The formatted value of the property</returns>
		public string GetFormattedValue(ModelProperty property)
		{
			return property.GetFormattedValue(this, null);
		}

		/// <summary>
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The formatted of the property</returns>
		public string GetFormattedValue(string property, string format)
		{
			return GetFormattedValue(Type.Properties[property], format);
		}

		/// <summary>
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The formatted of the property</returns>
		public string GetFormattedValue(string property, string format, IFormatProvider provider)
		{
			return GetFormattedValue(Type.Properties[property], format, provider);
		}

		/// <summary>
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelProperty"/></param>
		/// <returns>The formatted value of the property</returns>
		public string GetFormattedValue(ModelProperty property, string format)
		{
			return property.GetFormattedValue(this, format);
		}

		/// <summary>
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelProperty"/></param>
		/// <returns>The formatted value of the property</returns>
		public string GetFormattedValue(ModelProperty property, string format, IFormatProvider provider)
		{
			return property.GetFormattedValue(this, format, provider);
		}

		/// <summary>
		/// Gets the list of <see cref="ModelInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of property</param>
		/// <returns>The list of instances</returns>
		public ModelInstanceList GetList(string property)
		{
			return GetList(Type.References[property]);
		}

		/// <summary>
		/// Gets the list of <see cref="ModelInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelReferenceProperty"/></param>
		/// <returns>The list of instances</returns>
		public ModelInstanceList GetList(ModelReferenceProperty property)
		{
			return new ModelInstanceList(this, property);
		}

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		public void SetReference(string property, ModelInstance value)
		{
			SetReference(Type.References[property], value);
		}

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		public void SetReference(ModelReferenceProperty property, ModelInstance value)
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
		public void SetValue(ModelValueProperty property, object value)
		{
			if (property.AutoConvert && (value == null || property.Converter.CanConvertFrom(value.GetType())) )
				property.SetValue(instance, property.Converter.ConvertFrom(value));
			else
				property.SetValue(instance, value);
		}

		/// <summary>
		/// Saves changes to the current <see cref="ModelInstance"/> and all related 
		/// instances in the model.
		/// </summary>
		public void Save()
		{
			Type.SaveInstance(this);
		}

		/// <summary>
		/// Raises a custom event for the current instance.
		/// </summary>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="customEvent"></param>
		public void RaiseEvent<TEvent>(TEvent customEvent)
		{
			// Create a new model domain event and notify subscribers
			new ModelCustomEvent<TEvent>(this, customEvent).Notify();
		}

		/// <summary>
		/// Returns the string representation of the underlying model instance,
		/// potentially formatted using the default format specified by the model type.
		/// </summary>
		public override string ToString()
		{
			// Return a typed identifier for the instance if a format does not exist for the type
			if (String.IsNullOrEmpty(Type.Format))
				return Type.Name + "|" + Id;

			return ToString(Type.Format);
		}

		/// <summary>
		/// Returns the string representation of the underlying model instance,
		/// using the specified format.
		/// </summary>
		public string ToString(string format)
		{
			return ToString(format, null);
		}

		/// <summary>
		/// Returns the string representation of the underlying model instance,
		/// using the specified format.
		/// </summary>
		public string ToString(string format, IFormatProvider provider)
		{
			return ((IFormattable)this).ToString(format, provider);
		}

		/// <summary>
		/// Returns the string representation of the underlying model instance,
		/// using the specified format.
		/// </summary>
		/// <param name="format"></param>
		/// <returns></returns>
		public bool TryFormat(string format, out string value)
		{
			if (string.IsNullOrEmpty(format))
			{
				value = instance == null ? "" : instance.ToString();
				return true;
			}

			return Type.TryFormatInstance(this, format, out value);
		}

		/// <summary>
		/// Returns the string representation of the underlying model instance,
		/// using the specified format.
		/// </summary>
		/// <param name="format"></param>
		/// <param name="formatProvider"></param>
		/// <returns></returns>
		string IFormattable.ToString(string format, IFormatProvider formatProvider)
		{
			// Just return the string value of the underlying instance if a format was not specified
			if (String.IsNullOrEmpty(format))
				return Convert.ToString(instance);

			// Delegate to the type to format the instance
			return Type.FormatInstance(this, format);
		}

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> associated with the specified real instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public static ModelInstance GetModelInstance(object instance)
		{
			var modelInstance = instance as IModelInstance;
			if (modelInstance != null)
				return modelInstance.Instance;
			return ModelContext.Current.GetModelInstance(instance);
		}

		#endregion

		#region ReferenceSet

		/// <summary>
		/// Represents a list of in or out references maintained by a <see cref="ModelInstance"/>.
		/// </summary>
		/// <remarks>
		/// For performance reasons, the set is optimized to store single <see cref="ModelReference"/> instances
		/// without having to create a dictionary.
		/// </remarks>
		[Serializable]
		class ReferenceSet : IEnumerable<ModelReference>
		{
			ReferenceDirection direction;
			ModelReference reference;
			Dictionary<ModelInstance, ModelReference> referenceList;

			/// <summary>
			/// Creates a new <see cref="ReferenceSet"/> with the specified reference direction.
			/// </summary>
			/// <param name="direction"></param>
			internal ReferenceSet(ReferenceDirection direction)
			{
				this.direction = direction;
			}

			/// <summary>
			/// Gets the <see cref="ModelReference"/> that corresponds to the specified <see cref="ModelInstance"/>.
			/// </summary>
			/// <param name="instance"></param>
			/// <returns></returns>
			internal ModelReference this[ModelInstance instance]
			{
				get
				{
					if (this.referenceList != null)
					{
						ModelReference reference;
						this.referenceList.TryGetValue(instance, out reference);
						return reference;
					}
					else if (this.reference != null && instance == (direction == ReferenceDirection.In ? this.reference.In : this.reference.Out))
						return this.reference;
					return null;
				}
			}

			/// <summary>
			/// Adds the <see cref="ModelReference"/> to the current set.
			/// </summary>
			/// <param name="reference"></param>
			internal void Add(ModelReference reference)
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
					referenceList = new Dictionary<ModelInstance, ModelReference>();
					referenceList[direction == ReferenceDirection.In ? reference.In : reference.Out] = reference;

					// be sure not to double add references
					referenceList[direction == ReferenceDirection.In ? this.reference.In : this.reference.Out] = this.reference;
				}
			}

			/// <summary>
			/// Removes the <see cref="ModelReference"/> from the current set.
			/// </summary>
			/// <param name="reference"></param>
			internal void Remove(ModelReference reference)
			{
				if (referenceList != null)
					referenceList.Remove(direction == ReferenceDirection.In ? reference.In : reference.Out);
				else if (this.reference == reference)
					this.reference = null;
			}

			/// <summary>
			/// Enumerates over the <see cref="ModelReference"/> instances in the set.
			/// </summary>
			/// <returns></returns>
			public IEnumerator<ModelReference> GetEnumerator()
			{
				if (referenceList != null)
				{
					foreach (ModelReference r in referenceList.Values)
						yield return r;
				}
				else if (reference != null)
					yield return reference;
			}

			/// <summary>
			/// Enumerates over the <see cref="ModelReference"/> instances in the set.
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
			ModelInstance instance;
			ModelInstance destination;
			List<ModelPath> paths = new List<ModelPath>();
			Dictionary<Type, object> overrides = new Dictionary<Type, object>();
			List<FilterInfo> filters = new List<FilterInfo>();
			List<WhereInfo> wheres = new List<WhereInfo>();
			List<PathFuncInfo> pathFuncs = new List<PathFuncInfo>();
			Dictionary<ModelInstance, ModelInstance> maps = new Dictionary<ModelInstance,ModelInstance>();

			internal Cloner(ModelInstance instance, IEnumerable<string> paths)
				: this(instance, null, paths)
			{
			}

			internal Cloner(ModelInstance instance, ModelInstance destination, IEnumerable<string> paths)
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
			/// Specify delegate that will return a list of paths rooted at an instance
			/// of type <typeparamref name="TType"/> that will be used to instantiate
			/// new objects.  Usefull for specifying additional paths for dynamic types
			/// that may be encountered some distance from the root <see cref="ModelInstance"/>
			/// of this <see cref="Cloner"/>.
			/// </summary>
			public Cloner Clone<TType>(Func<ModelStep, TType, IEnumerable<string>> pathFunc)
			{
				pathFuncs.Add(new PathFuncInfo<TType> { PathFunc = pathFunc });
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
			/// <see cref="ModelStep"/>, original instance (<typeparamref name="TType"/>),
			/// and original value (<typeparamref name="TValue"/>) of the 
			/// property or list member.
			/// </summary>
			/// <typeparam name="TType">Declaring type of property to copy</typeparam>
			/// <typeparam name="TValue">Property type of property to copy</typeparam>
			/// <param name="where">returns true when a new instance of <typeparamref name="TValue"/>
			/// should be instantiated.</param>
			public Cloner Where<TType, TValue>(Func<ModelStep, TType, TValue, bool> where)
			{
				wheres.Add(new WhereInfo<TType, TValue> { When = where });
				return this;
			}

			/// <summary>
			/// Conditionally determine whether to copy a value, given the specific property or list member.  
			/// Filtered values will neither be used to instantiate new objects
			/// nor be copied as references on the new model.
			/// </summary>
			/// <param name="filter">returns true when the property
			/// should be considered for the cloning process.</param>
			/// <returns></returns>
			public Cloner Filter(Func<ModelProperty, bool> filter)
			{
				filters.Add(new FilterInfo { When = filter });
				return this;
			}

			/// <summary>
			/// Conditionally determine whether to copy a value, given the specific
			/// <see cref="ModelProperty"/>, original instance (<typeparamref name="TType"/>),
			/// and original value (<typeparamref name="TValue"/>) of the 
			/// property or list member.  Filtered values will neither be used to instantiate new objects
			/// nor be copied as references on the new model.
			/// </summary>
			/// <typeparam name="TType">Declaring type of property to copy</typeparam>
			/// <typeparam name="TValue">Property type of property to copy</typeparam>
			/// <param name="filter">returns true when the value of <typeparamref name="TValue"/>
			/// should be considered for the cloning process.</param>
			/// <returns></returns>
			public Cloner Filter<TType, TValue>(Func<ModelProperty, TType, TValue, bool> filter)
			{
				filters.Add(new FilterInfo<TType, TValue> { When = filter });
				return this;
			}

			/// <summary>
			/// Adds a mapping between a source <see cref="ModelInstance"/> and a destination
			/// <see cref="ModelInstance"/>.
			/// 
			/// If paths provided to the cloner cause <paramref name="from"/> to have been cloned then
			/// the cloned <see cref="ModelInstance"/> will be copied, and not the <paramref name="to"/>
			/// that may have been provided here.
			/// </summary>
			/// <param name="from">A <see cref="ModelInstance"/> in the source model.</param>
			/// <param name="to">The <see cref="ModelInstance"/> that will be copied instead.</param>
			/// <returns></returns>
			public Cloner Map(ModelInstance from, ModelInstance to)
			{
				maps.Add(from, to);
				return this;
			}

			/// <summary>
			/// Begins the cloning process and returns the copied <see cref="ModelInstance"/>
			/// of the original <see cref="ModelInstance"/>
			/// </summary>
			/// <returns></returns>
			public ModelInstance Invoke()
			{
				ModelInstance result = null;
				ModelEventScope.Perform(() =>
				{
					// Clone instances
					var clones = new Dictionary<ModelInstance, ModelInstance>();

					if (destination != null)
						clones.Add(instance, destination);

					foreach (var p in paths)
						instance.CloneInstance(p.FirstSteps, clones, filters, wheres, pathFuncs);

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
				internal abstract bool Allows(ModelStep step, object item, object value);
			}

			class WhereInfo<TType, TValue> : WhereInfo
			{
				internal Func<ModelStep, TType, TValue, bool> When { get; set; }

				internal override bool Allows(ModelStep step, object instance, object value)
				{
					if (instance is TType && value is TValue)
						return When(step, (TType)instance, (TValue)value);

					// where doesn't even apply
					return true;
				}
			}

			internal class FilterInfo
			{
				internal Func<ModelProperty, bool> When { get; set; }

				internal virtual bool Allows(ModelProperty property, object item, object value)
				{
					return When(property);
				}
			}

			class FilterInfo<TType, TValue> : FilterInfo
			{
				internal new Func<ModelProperty, TType, TValue, bool> When { get; set; }

				internal override bool Allows(ModelProperty property, object instance, object value)
				{
					if (instance is TType && value is TValue)
						return When(property, (TType)instance, (TValue)value);

					// filter doesn't even apply
					return true;
				}
			}

			internal abstract class PathFuncInfo
			{
				internal abstract IEnumerable<string> GetPaths(ModelStep step, object instance);
			}

			class PathFuncInfo<TType> : PathFuncInfo
			{
				internal Func<ModelStep, TType, IEnumerable<string>> PathFunc { get; set; }

				internal override IEnumerable<string> GetPaths(ModelStep step, object instance)
				{
					if (instance is TType)
						foreach (string path in PathFunc(step,(TType)instance))
							yield return path;
				}
			}
		}

		#endregion
	}
}
