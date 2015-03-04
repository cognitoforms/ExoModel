using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq.Expressions;

namespace ExoModel
{
	/// <summary>
	/// Abstract base class for classes that represent a specific type in a model hierarchy.
	/// </summary>
	[Serializable]
	public abstract class ModelType : ISerializable, IModelPropertySource
	{
		#region Fields

		internal static ModelType Unknown = new UnknownModelType();
		static Regex formatParser = new Regex(@"(?<!\\)\[(?<property>[a-z0-9_.]+)(?:\:(?<format>.+?))?(?<!\\)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		Dictionary<Type, object> customEvents = new Dictionary<Type, object>();
		Dictionary<Type, object> transactedCustomEvents = new Dictionary<Type, object>();
		Dictionary<string, List<FormatToken>> formats = new Dictionary<string, List<FormatToken>>();
		Attribute[] attributes;
		Dictionary<Type, object> extensions;

		IList<Action> initializers = new List<Action>();
		HashSet<string> invalidPaths = new HashSet<string>();
		bool isInitialized = false;
		#endregion

		#region Contructors

		/// <summary>
		/// Initializes the <see cref="ModelType"/> when called by concrete subclasses.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="qualifiedName"></param>
		/// <param name="baseType"></param>
		/// <param name="scope"></param>
		/// <param name="attributes"></param>
		public ModelType(string name, string qualifiedName, ModelType baseType, string scope, string format, Attribute[] attributes)
		{
			this.Name = name;
			this.QualifiedName = qualifiedName;
			this.Scope = scope;
			this.Format = format;
			this.attributes = attributes;
			this.BaseType = baseType;

			// Initialize list properties
			this.SubTypes = new ModelTypeList();
			this.Properties = new ModelPropertyList();
			this.Methods = new ModelMethodList();
			this.References = new ModelReferencePropertyList();
			this.Values = new ModelValuePropertyList();
			this.Paths = new ModelPathList();
			this.Expressions = new Dictionary<string, ModelExpression>();
		}

		#endregion

		#region Properties

		public ModelContext Context { get; protected set; }

		public string Name { get; private set; }

		public string Format { get; private set; }

		public string QualifiedName { get; private set; }

		public string Scope { get; private set; }

		public ModelType BaseType { get; private set; }

		public ModelTypeList SubTypes { get; private set; }

		public ModelPropertyList Properties { get; private set; }

		public ModelMethodList Methods { get; private set; }

		internal ModelReferencePropertyList References { get; private set; }

		internal ModelValuePropertyList Values { get; private set; }

		internal int PropertyCount { get; private set; }

		public IModelTypeProvider Provider { get; internal set; }

		ModelPathList Paths { get; set; }

		Dictionary<string, ModelExpression> Expressions { get; set; }

		#endregion

		#region Events

		public event EventHandler<ModelInitEvent> Init;
		public event EventHandler<ModelDeleteEvent> Delete;
		public event EventHandler<ModelPropertyGetEvent> PropertyGet;
		public event EventHandler<ModelReferenceChangeEvent> ReferenceChange;
		public event EventHandler<ModelValueChangeEvent> ValueChange;
		public event EventHandler<ModelListChangeEvent> ListChange;
		public event EventHandler<ModelSaveEvent> Save;

		#endregion

		#region Methods

		/// <summary>
		/// Gets or creates an extension instance linked to the current <see cref="ModelType"/>.
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
			// First see if the type is ICollection<T>
			if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(ICollection<>))
			{
				itemType = listType.GetGenericArguments()[0];
				return true;
			}

			// Then see if the type implements ICollection<T>
			foreach (Type interfaceType in listType.GetInterfaces())
			{
				if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(ICollection<>))
				{
					itemType = interfaceType.GetGenericArguments()[0];
					return true;
				}
			}

			// Then see if the type implements IList and has a strongly-typed Item property indexed by an integer value
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
		/// Performs one time initialization on the <see cref="ModelType"/> when it is registered
		/// with the <see cref="ModelContext"/>.
		/// </summary>
		/// <param name="context"></param>
		protected internal void Initialize(ModelContext context)
		{
			if (isInitialized) return;

			// Set the context the model type is registered with
			this.Context = context;

			// Set the next property index for properties added inside OnInit
			PropertyCount = BaseType == null ? 0 : BaseType.PropertyCount;

			// Allow subclasses to perform initialization, such as adding properties
			OnInit();

			// Fire after-initialization logic
			foreach (var initializer in initializers)
				initializer();

			// Add to base type after all other initialization is complete
			if (BaseType != null && Provider != null && Provider.IsCachable)
			{
				ModelType subType = BaseType.SubTypes[this.Name];
				if (subType == null)
					BaseType.SubTypes.Add(this);
			}

			isInitialized = true;
		}

		/// <summary>
		/// Allow types to preform post-initialization logic
		/// </summary>
		/// <param name="afterInit"></param>
		public void AfterInitialize(Action afterInit)
		{
			if (Context != null)
				afterInit();
			else
				initializers.Add(afterInit);
		}

		/// <summary>
		/// Overriden by subclasses to perform type initialization, specifically including
		/// setting the base type and adding properties.  This initialization must occur inside this
		/// method and not in the constructor to ensure that base types are completely initialized before
		/// their child types.
		/// </summary>
		protected internal abstract void OnInit();

		/// <summary>
		/// Adds the specified property to the current model type.
		/// </summary>
		/// <param name="property"></param>
		protected void AddProperty(ModelProperty property)
		{
			if (property.DeclaringType == this)
				property.Index = PropertyCount++;

			if (property is ModelReferenceProperty)
				References.Add((ModelReferenceProperty)property);
			else
				Values.Add((ModelValueProperty)property);

			Properties.Add(property);
		}

		/// <summary>
		/// Adds the specified method to the current model type.
		/// </summary>
		/// <param name="method"></param>
		protected void AddMethod(ModelMethod method)
		{
			Methods.Add(method);
		}

		internal void RaiseInit(ModelInitEvent initEvent)
		{
			if (Init != null)
				Init(this, initEvent);
		}

		internal void RaiseDelete(ModelDeleteEvent deleteEvent)
		{
			if (Delete != null)
				Delete(this, deleteEvent);
		}

		internal bool PropertyGetHasSubscriptions
		{
			get
			{
				return PropertyGet != null;
			}
		}

		internal void RaisePropertyGet(ModelPropertyGetEvent propertyGetEvent)
		{
			if (PropertyGet != null)
				PropertyGet(this, propertyGetEvent);
		}

		internal void RaiseReferenceChange(ModelReferenceChangeEvent referenceChangeEvent)
		{
			if (ReferenceChange != null)
				ReferenceChange(this, referenceChangeEvent);
		}

		internal void RaiseValueChange(ModelValueChangeEvent valueChangeEvent)
		{
			if (ValueChange != null)
				ValueChange(this, valueChangeEvent);
		}

		internal void RaiseListChange(ModelListChangeEvent listChangeEvent)
		{
			if (ListChange != null)
				ListChange(this, listChangeEvent);
		}

		internal void RaiseSave(ModelSaveEvent modelSaveEvent)
		{
			if (Save != null)
				Save(this, modelSaveEvent);
		}

		/// <summary>
		/// Defines the delegate the custom event handlers must implement to subscribe.
		/// </summary>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="instance"></param>
		/// <param name="event"></param>
		public delegate void CustomEvent<TEvent>(ModelInstance instance, TEvent @event);

		/// <summary>
		/// Adds a custom event handler for a specific custom event raised by the current model type.
		/// </summary>
		/// <typeparam name="TEvent">
		/// The type of the custom event parameter that will be passed
		/// as an argument when the custom event is raised
		/// </typeparam>
		/// <param name="handler">The event handler for the custom event</param>
		public void Subscribe<TEvent>(CustomEvent<TEvent> handler)
		{
			object currentHandler;

			if (typeof(ITransactedModelEvent).IsAssignableFrom(typeof(TEvent)))
				transactedCustomEvents[typeof(TEvent)] =
					transactedCustomEvents.TryGetValue(typeof(TEvent), out currentHandler) ?
					(CustomEvent<TEvent>)currentHandler + handler : handler;
			else
				customEvents[typeof(TEvent)] =
						customEvents.TryGetValue(typeof(TEvent), out currentHandler) ?
						(CustomEvent<TEvent>)currentHandler + handler : handler;
		}

		/// <summary>
		/// Removes a custom event handler for a specific custom event raised by the current model type.
		/// </summary>
		/// <typeparam name="TDomainEvent">
		/// The type of the custom event parameter that will be passed
		/// as an argument when the domain event is raised
		/// </typeparam>
		/// <param name="handler">The event handler for the custom event</param>
		public void Unsubscribe<TEvent>(CustomEvent<TEvent> handler)
		{
			object currentHandler;
			if (typeof(ITransactedModelEvent).IsAssignableFrom(typeof(TEvent)))
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
		internal void RaiseEvent<TEvent>(ModelCustomEvent<TEvent> customEvent)
		{
			object currentHandler;
			if (customEvents.TryGetValue(typeof(TEvent), out currentHandler))
				((CustomEvent<TEvent>)currentHandler)(customEvent.Instance, customEvent.CustomEvent);
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
		/// Gets the first occurence of an attribute.  Optionally searches base classes
		/// </summary>
		/// <typeparam name="TAttribute">The type of attribute to locate</typeparam>
		/// <param name="inherit">If true, base types will be searched</param>
		/// <returns>The first matching attribute</returns>
		public TAttribute GetAttribute<TAttribute>(bool inherit)
			where TAttribute : Attribute
		{
			for (ModelType t = this; inherit && t != null; t = t.BaseType)
			{
				var attribute = t.GetAttributes<TAttribute>().FirstOrDefault();

				if (attribute != null)
					return attribute;
			}

			return null;
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
		/// Gets the <see cref="ModelPath"/> starting from the current <see cref="ModelType"/> based
		/// on the specified path string.
		/// </summary>
		/// <param name="path"></param>
		/// <returns>The requested <see cref="ModelPath"/></returns>
		public ModelPath GetPath(string path)
		{
			ModelPath modelPath;
			if (!TryGetPath(path, out modelPath))
				throw new ArgumentException("The specific path could not be evaluated: " + path);
			return modelPath;
		}

		/// <summary>
		/// Gets the <see cref="ModelPath"/> starting from the current <see cref="ModelType"/> based
		/// on the specified <see cref="Expression"/> tree.
		/// </summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public ModelPath GetPath<TRoot>(Expression<Action<TRoot>> expression)
		{
			return ModelPath.CreatePath(this, expression);
		}

		/// <summary>
		/// Gets the <see cref="ModelPath"/> starting from the current <see cref="ModelType"/> based
		/// on the specified <see cref="Expression"/> tree.
		/// </summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public ModelPath GetPath<TRoot, TResult>(Expression<Func<TRoot, TResult>> expression)
		{
			return ModelPath.CreatePath(this, expression);
		}

		/// <summary>
		/// Gets the <see cref="ModelPath"/> starting from the current <see cref="ModelType"/> based
		/// on the specified <see cref="Expression"/> tree.
		/// </summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public ModelPath GetPath(Expression expression)
		{
			return ModelPath.CreatePath(this, expression);
		}

		/// <summary>
		/// Gets a <see cref="ModelExpression"/> for the specified expression string.
		/// </summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public ModelExpression GetExpression(string expression)
		{
			return GetExpression(typeof(object), expression);
		}

		/// <summary>
		/// Gets a <see cref="ModelExpression"/> for the specified expression string.
		/// </summary>
		/// <param name="expression"></param>
		/// <returns></returns>
		public ModelExpression GetExpression<TResult>(string expression, ModelExpression.QuerySyntax querySyntax = ModelExpression.QuerySyntax.DotNet)
		{
			return GetExpression(typeof(TResult), expression, querySyntax);
		}

		/// <summary>
		/// Gets a <see cref="ModelExpression"/> for the specified expression string.
		/// </summary>
		/// <param name="expression"></param>
		/// <param name="resultType"></param>
		/// <returns></returns>
		public ModelExpression GetExpression(Type resultType, string expression, ModelExpression.QuerySyntax querySyntax = ModelExpression.QuerySyntax.DotNet)
		{
			ModelExpression exp;
			if (!Expressions.TryGetValue(expression, out exp))
				Expressions[expression] = exp = new ModelExpression(this, expression, resultType, querySyntax);
			return exp;
		}


		public bool TryGetExpression<TResult>(string expression, out ModelExpression modelExpression, ModelExpression.QuerySyntax querySyntax = ModelExpression.QuerySyntax.DotNet)
		{
			return TryGetExpression(typeof(TResult), expression, out modelExpression, querySyntax);
		}

		public bool TryGetExpression(Type resultType, string expression, out ModelExpression modelExpression, ModelExpression.QuerySyntax querySyntax = ModelExpression.QuerySyntax.DotNet)
		{
			try
			{
				modelExpression = GetExpression(resultType, expression, querySyntax);
				return true;
			}
			catch
			{
				modelExpression = null;
				return false; 
			}
		}

		/// <summary>
		///  Gets the <see cref="ModelPath"/> starting from the current <see cref="ModelType"/> based
		/// on the specified path string.
		/// </summary>
		/// <param name="path"></param>
		/// <param name="modelPath"></param>
		/// <returns>True if the path is valid and was returned as an output parameter, otherwise false.</returns>
		public bool TryGetPath(string path, out ModelPath modelPath)
		{
			try
			{	
				// First see if the path has already been created for this instance type
				path = path.Replace(" ", "");
				modelPath = Paths[path];
				if (modelPath != null)
					return true;

				if (invalidPaths.Contains(path))
					return false;

				// Otherwise, create and cache a new path
				modelPath = ModelPath.CreatePath(this, path);
				if (modelPath == null)
				{
					invalidPaths.Add(path);
					return false;
				}

				Paths.Add(modelPath);
				return true;
			}
			catch (Exception ex)
			{
				throw new ApplicationException(string.Format("Error trying to get path '{0}' on type '{1}': [{2}]",
					path,
					Name ?? "UNKNOWN",
					ex.Message),
				ex);
			}
		}

		/// <summary>
		/// Returns the current type and all ancestor base types.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<ModelType> GetAncestorsInclusive()
		{
			for (ModelType type = this; type != null; type = type.BaseType)
				yield return type;
		}

		/// <summary>
		/// Returns the current type and all descendent sub types.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public IEnumerable<ModelType> GetDescendentsInclusive()
		{
			yield return this;
			foreach (var subType in SubTypes)
				foreach (var descendentType in subType.GetDescendentsInclusive())
					yield return descendentType;
		}

		/// <summary>
		/// Indicates whether the specified <see cref="ModelInstance"/> is either of the current type
		/// or of a sub type of the current type.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public bool IsInstanceOfType(ModelInstance instance)
		{
			ModelType instanceType = instance.Type;
			ModelType currentType = this;
			while (instanceType != null)
			{
				if (instanceType == currentType)
					return true;
				instanceType = instanceType.BaseType;
			}
			return false;
		}

		/// <summary>
		/// Indicates whether the specified type is a subtype of the current type.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public bool IsSubType(ModelType type)
		{
			while (type != null)
			{
				if (type.BaseType == this)
					return true;
				type = type.BaseType;
			}
			return false;
		}

		/// <summary>
		/// Creates a new instance of the current <see cref="ModelType"/>.
		/// </summary>
		/// <returns></returns>
		public ModelInstance Create()
		{
			return GetModelInstance(GetInstance(null));
		}

		/// <summary>
		/// Creates an existing instance of the current <see cref="ModelType"/>.
		/// </summary>
		/// <returns></returns>
		public ModelInstance Create(string id)
		{
			object instance = GetInstance(id);
			return instance == null ? null : GetModelInstance(instance);
		}

		/// <summary>
		/// Returns the name of the model type.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Name;
		}

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		public ModelInstance GetReference(string property)
		{
			return GetReference(References[property]);
		}

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelReferenceProperty"/></param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		public ModelInstance GetReference(ModelReferenceProperty property)
		{
			object reference = property.GetValue(null);
			if (reference != null)
				return GetModelInstance(reference);
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
		/// <param name="property">The specific <see cref="ModelValueProperty"/></param>
		/// <returns>The value of the property</returns>
		public object GetValue(ModelValueProperty property)
		{
			if (property.AutoConvert)
				return property.Converter.ConvertTo(property.GetValue(null), typeof(object));
			else
				return property.GetValue(null);
		}

		/// <summary>
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The formatted of the property</returns>
		public string GetFormattedValue(string property, string format)
		{
			return GetFormattedValue(Properties[property], format);
		}

		/// <summary>
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelProperty"/></param>
		/// <returns>The formatted value of the property</returns>
		public string GetFormattedValue(ModelProperty property, string format)
		{
			return property.GetFormattedValue(null, format);
		}

		/// <summary>
		/// Gets the list of <see cref="ModelInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of property</param>
		/// <returns>The list of instances</returns>
		public ModelInstanceList GetList(string property)
		{
			return GetList(References[property]);
		}

		/// <summary>
		/// Gets the list of <see cref="ModelInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelReferenceProperty"/></param>
		/// <returns>The list of instances</returns>
		public ModelInstanceList GetList(ModelReferenceProperty property)
		{
			return new ModelInstanceList(null, property);
		}

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		public void SetReference(string property, ModelInstance value)
		{
			SetReference(References[property], value);
		}

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		public void SetReference(ModelReferenceProperty property, ModelInstance value)
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
		public void SetValue(ModelValueProperty property, object value)
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
		/// <returns>The underlying value of the property in the physical model</returns>
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
		/// <param name="property">The <see cref="ModelProperty"/> to get or set</param>
		/// <returns>The underlying value of the property in the physical model</returns>
		public object this[ModelProperty property]
		{
			get
			{
				return property is ModelValueProperty ? GetValue((ModelValueProperty)property) : property.GetValue(null);
			}
			set
			{
				if (property is ModelValueProperty)
					SetValue((ModelValueProperty)property, value);
				else
					property.SetValue(null, value);
			}
		}

		/// <summary>
		/// Converts the specified object into a instance that implements <see cref="IList"/>.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="list"></param>
		/// <returns></returns>
		protected internal virtual IList ConvertToList(ModelReferenceProperty property, object list)
		{
			if (list == null)
				return null;

			if (list is IList)
				return (IList)list;

			throw new NotSupportedException("Unable to convert the specified list instance into a valid IList implementation.");
		}

		/// <summary>
		/// Bulk updates the values of a list reference property to have the values of the specified enumeration.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <param name="values"></param>
		/// <remarks>Subclasses may override this implementation to support bulk list updates</remarks>
		protected internal virtual void UpdateList(ModelInstance instance, ModelReferenceProperty property, IEnumerable<ModelInstance> values)
		{
			// Get the source list
			var source = instance.GetList(property);

			// Attempt the handle cases where the underlying list property is null
			if (source.GetList() == null)
				InitializeList(instance, property);

			// Get the set of items the list should contain
			var items = new HashSet<ModelInstance>(values);

			// Remove items from the source that do not exist in the calculated set
			foreach (var item in source.ToArray())
			{
				if (!items.Contains(item))
					source.Remove(item);
				else
					items.Remove(item);
			}

			// Add items that did not exist in the source
			foreach (var item in items)
				source.Add(item);
		}

		/// <summary>
		/// Initializes a <see cref="ModelReferenceProperty"/> on the specified instance to a new empty list.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		protected internal virtual void InitializeList(ModelInstance instance, ModelReferenceProperty property)
		{
			throw new NotSupportedException("Initialization of null list properties is not supported by default.  Override ModelType.InitializeList to enable this behavior or ensure all list properties always have a value list value.");
		}

		protected internal virtual void OnStartTrackingList(ModelInstance instance, ModelReferenceProperty property, IList list)
		{
		}

		protected internal virtual void OnStopTrackingList(ModelInstance instance, ModelReferenceProperty property, IList list)
		{
		}

		protected internal void OnPropertyChanged(ModelInstance instance, ModelProperty property, object oldValue, object newValue)
		{
			// Static notifications are not supported
			// Ignore objects after deletion
			if (property.IsStatic || instance.IsDeleted)
				return;

			// Check to see what type of property was changed
			if (property is ModelReferenceProperty)
			{
				ModelReferenceProperty reference = (ModelReferenceProperty)property;

				// Changes to list properties should call OnListChanged. However, some implementations
				// may allow setting lists, so this case must be handled appropriately.
				if (reference.IsList)
				{
					// Notify the context that the items in the old list have been removed
					if (oldValue != null)
					{
						var oldList = ConvertToList(reference, oldValue);

						if (!reference.PropertyType.IsCached(oldList))
						{
							OnListChanged(instance, reference, null, oldList);
							OnStopTrackingList(instance, reference, oldList);
						}
					}

					// Then notify the context that the items in the new list have been added
					if (newValue != null)
					{
						var newList = ConvertToList(reference, newValue);
						if (!reference.PropertyType.IsCached(newList))
						{
							OnListChanged(instance, reference, newList, null);
							OnStartTrackingList(instance, reference, newList);
						}
					}
				}

				// Notify subscribers that a reference property has changed
				else
				{
					var modelRefProp = (ModelReferenceProperty)property;
					new ModelReferenceChangeEvent(
						instance, modelRefProp,
						oldValue == null ? null : modelRefProp.PropertyType.GetModelInstance(oldValue),
						newValue == null ? null : modelRefProp.PropertyType.GetModelInstance(newValue)
					).Notify();
				}
			}

			// Otherwise, notify subscribers that a value property has changed
			else
				new ModelValueChangeEvent(instance, (ModelValueProperty)property, oldValue, newValue).Notify();
		}

		protected void OnListChanged(ModelInstance instance, string property, IEnumerable added, IEnumerable removed)
		{
			OnListChanged(instance, (ModelReferenceProperty)instance.Type.Properties[property], added, removed);
		}

		protected void OnListChanged(ModelInstance instance, ModelReferenceProperty property, IEnumerable added, IEnumerable removed)
		{
			// Static notifications and notifications during load are not supported
			// Ignore objects after deletion
			if (property.IsStatic || instance.IsDeleted)
				return;

			// Create a new model list change event and notify subscribers
			new ModelListChangeEvent(instance, property, EnumerateInstances(added), EnumerateInstances(removed)).Notify();
		}

		IEnumerable<ModelInstance> EnumerateInstances(IEnumerable items)
		{
			if (items != null)
				foreach (object instance in items)
					yield return GetModelInstance(instance);
		}

		/// <summary>
		/// Called by subclasses to notify the context that a commit has occurred.
		/// </summary>
		/// <param name="instance"></param>
		protected void OnSave(ModelInstance instance)
		{
			new ModelSaveEvent(instance).Notify();
		}

		/// <summary>
		/// Called by subclasses to notify the context that an instance's pending deletion status has changed.
		/// </summary>
		/// <param name="instance"></param>
		protected internal void OnPendingDelete(ModelInstance instance)
		{
			new ModelDeleteEvent(instance, instance.IsPendingDelete).Notify();
		}

		/// <summary>
		/// Saves changes to the specified instance and related instances in the model.
		/// </summary>
		/// <param name="modelInstance"></param>
		protected internal abstract void SaveInstance(ModelInstance modelInstance);

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> associated with the specified real instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public abstract ModelInstance GetModelInstance(object instance);

		/// <summary>
		/// Gets the unique string identifier of an existing instance, or null for new instances.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected internal abstract string GetId(object instance);

		/// <summary>
		/// Gets the existing instance with the specified string identifier.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		protected internal abstract object GetInstance(string id);

		/// <summary>
		/// Gets the underlying modification status of the specified instance,
		/// indicating whether the instance has pending changes that have not been
		/// persisted.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns>True if the instance is new, pending delete, or has unpersisted changes, otherwise false.</returns>
		protected internal abstract bool GetIsModified(object instance);

		/// <summary>
		/// Gets the deletion status of the specified instance indicating whether
		/// the instance has been permanently deleted.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected internal abstract bool GetIsDeleted(object instance);

		/// <summary>
		/// Indicates whether the specified instance is pending deletion.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected internal abstract bool GetIsPendingDelete(object instance);

		/// <summary>
		/// Sets whether the specified instance is pending deletion.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="isPendingDelete"></param>
		protected internal abstract void SetIsPendingDelete(object instance, bool isPendingDelete);

		/// <summary>
		/// Gets a format provider to provide custom formatting services for the specified type.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		protected internal IFormatProvider GetFormatProvider(Type type)
		{
			if (type == typeof(bool) || type == typeof(bool?))
				return BooleanFormatter.Instance;
			return null;
		}

		/// <summary>
		/// Indicates whether the specified instance is cached and should be prevented from maintaining 
		/// references to <see cref="ModelContext"/>.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected internal virtual bool IsCached(object instance)
		{
			return false;
		}

		/// <summary>
		/// Acquires a lock that can be used to synchronize multi-threaded access.  Its safe to assume
		/// that IsCached(instance) is true.
		/// </summary>
		protected internal virtual void EnterLock(object instance, out bool acquired)
		{
			throw new InvalidOperationException(this.GetType() + " must implement EnterLock() if IsCached() is overridden");
		}

		/// <summary>
		/// Releases a lock that acquired by calling EnterLock
		/// </summary>
		protected internal virtual void ExitLock(object instance, bool acquired)
		{
			throw new InvalidOperationException(this.GetType() + " must implement ExitLock() if EnterLock() is overridden");
		}

		/// <summary>
		/// Attempts to format the instance using the specified format.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		internal bool TryFormatInstance(ModelInstance instance, string format, out string value)
		{
			List<FormatToken> formatTokens;
			bool hasError = TryGetFormatTokens(format, out formatTokens);

			// Handle simple case of [Property]
			if (formatTokens.Count == 1 && formatTokens[0].Literal == null)
			{
				value = formatTokens[0].Property.GetFormattedValue(instance, formatTokens[0].Format);
				return true;
			}

			// Use a string builder to create and return the formatted result
			StringBuilder result = new StringBuilder();
			foreach (var token in formatTokens)
			{
				result.Append(token.Literal);
				if (token.Property != null)
					result.Append(token.Property.GetFormattedValue(instance, token.Format));
			}
			value = result.ToString();

			return !hasError;
		}

		/// <summary>
		/// Attempts to format the instance using the specified format.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		bool TryGetFormatTokens(string format, out List<FormatToken> formatTokens)
		{
			bool hasError = false;

			// Get the list of format tokens by parsing the format expression
			if (!formats.TryGetValue(format, out formatTokens))
			{
				// Replace \\ escape sequence with char 0, \[ escape sequence with char 1, and \] escape sequence with char 2
				var escapedFormat = format.Replace(@"\\", ((char)0).ToString()).Replace(@"\[", ((char)1).ToString()).Replace(@"\]", ((char)1).ToString());

				// Replace \\ escape sequence with \, \[ escape sequence with [, and \] escape sequence with ]
				var correctedFormat = format.Replace(@"\\", @"\").Replace(@"\[", "[").Replace(@"\]", "]");

				formatTokens = new List<FormatToken>();
				int index = 0;
				foreach (Match substitution in formatParser.Matches(escapedFormat))
				{
					var path = substitution.Groups["property"].Value;
					ModelPath modelPath;
					if (!TryGetPath(path, out modelPath))
					{
						hasError = true;
						formatTokens.Add(new FormatToken() { Literal = correctedFormat.Substring(index, substitution.Index - index) });
					}
					else
						formatTokens.Add(
							new FormatToken()
							{
								Literal = substitution.Index > index ? correctedFormat.Substring(index, substitution.Index - index) : null,
								Property = new ModelSource(modelPath),
								Format = substitution.Groups["format"].Success ? correctedFormat.Substring(substitution.Groups["format"].Index, substitution.Groups["format"].Length) : null
							});

					index = substitution.Index + substitution.Length;
				}
				// Add the trailing literal
				if (index < correctedFormat.Length)
					formatTokens.Add(new FormatToken() { Literal = correctedFormat.Substring(index, correctedFormat.Length - index) });

				// Cache the parsed format expression
				if (!hasError)
					formats.Add(format, formatTokens);
			}

			return hasError;
		}

		/// <summary>
		/// Adds model steps to the specified root step based on the specified instance format
		/// </summary>
		/// <param name="format"></param>
		/// <param name="rootStep"></param>
		internal void AddFormatSteps(ModelStep rootStep, string format)
		{
			List<FormatToken> formatTokens;
			TryGetFormatTokens(format, out formatTokens);
			foreach (var token in formatTokens)
			{
				ModelStep modelStep = rootStep;
				foreach (var sourceStep in token.Property.Steps)
				{
					modelStep = new ModelStep(rootStep.Path) { PreviousStep = modelStep, Property = Context.GetModelType(sourceStep.DeclaringType).Properties[sourceStep.Property] };
					modelStep.PreviousStep.NextSteps.Add(modelStep);
				}
			}
		}

		/// <summary>
		/// Formats the instance using the specified format.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		internal string FormatInstance(ModelInstance instance, string format)
		{
			string result;
			if (!TryFormatInstance(instance, format, out result))
				throw new ArgumentException("The specified format, '" + format + "', was not valid for the root type of '" + instance.Type.Name + "'.");
			return result;
		}

		#endregion

		#region FormatToken

		/// <summary>
		/// Represents a token portion of a model instance format expression.
		/// </summary>
		class FormatToken
		{
			internal string Literal { get; set; }

			internal ModelSource Property { get; set; }

			internal string Format { get; set; }

			public override string ToString()
			{
				return Literal + (Property == null ? "" : "[" + Property.Path + (String.IsNullOrEmpty(Format) ? "" : ":" + Format) + "]");
			}
		}

		#endregion

		#region BooleanFormatter

		/// <summary>
		/// Implementation of <see cref="IFormatProvider"/> that supports using format specifiers
		/// for <see cref="Boolean"/> properties by default.  The usage of this class can be eliminated
		/// by overriding the behavior of <see cref="GetFormatProvider"/>.
		/// </summary>
		private class BooleanFormatter : IFormatProvider, ICustomFormatter
		{
			internal static BooleanFormatter Instance = new BooleanFormatter();

			object IFormatProvider.GetFormat(Type formatType)
			{
				if (formatType == typeof(ICustomFormatter))
					return this;
				return null;
			}

			string ICustomFormatter.Format(string format, object arg, IFormatProvider formatProvider)
			{
				if (String.IsNullOrEmpty(format))
					return arg + "";
				var options = format.Split(';');
				if (arg == null && options.Length >= 3)
					return options[2]; // Unspecified
				else if (((arg is bool && (bool)arg) || (arg is bool? && ((bool?)arg).HasValue && ((bool?)arg).Value)) && options.Length >= 1)
					return options[0]; // True
				else if (options.Length >= 2)
					return options[1]; // False
				return null;
			}
		}

		#endregion

		#region ISerializable Members
		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.SetType(typeof(Serialized));
			info.AddValue("name", Name);
		}

		[Serializable]
		private class Serialized : ISerializable, IObjectReference
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
				return typeName == "ExoModel.ModelType.Unknown" ? ModelType.Unknown : ModelContext.Current.GetModelType(typeName);
			}
			#endregion
		}
		#endregion

		#region UnknownModelType

		/// <summary>
		/// Private class representing a model type that has not been determined, which is necessary when
		/// initializing model instances without forcing the type of the instance to be determined before
		/// the underlying instance is fully constructed.  This is a marker type that should never be returned
		/// or accessed by external code.
		/// </summary>
		[Serializable]
		class UnknownModelType : ModelType
		{
			internal UnknownModelType()
			   : base("ExoModel.ModelType.Unknown", "ExoModel.ModelType.Unknown", null, "Unknown", null, new Attribute[] { })
			{ }

			protected internal override void OnInit()
			{
				throw new NotSupportedException();
			}

			protected internal override IList ConvertToList(ModelReferenceProperty property, object list)
			{
				throw new NotSupportedException();
			}

			protected internal override void SaveInstance(ModelInstance modelInstance)
			{
				throw new NotSupportedException();
			}

			public override ModelInstance GetModelInstance(object instance)
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

			protected internal override bool GetIsDeleted(object instance)
			{
				throw new NotSupportedException();
			}

			protected internal override bool GetIsModified(object instance)
			{
				throw new NotSupportedException();
			}

			protected internal override bool GetIsPendingDelete(object instance)
			{
				throw new NotSupportedException();
			}

			protected internal override void SetIsPendingDelete(object instance, bool isPendingDelete)
			{
				throw new NotSupportedException();
			}
		}

		#endregion
	}
}
