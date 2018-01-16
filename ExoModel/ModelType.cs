using System;
using System.Diagnostics;
using System.Globalization;
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
		// Regex to find all format tokesn, the pattern matches all letters and digits that are valid for javascript identifiers, including "_" and "." inside square brackets
		public static readonly Regex FormatParser = new Regex(@"(?<!\\)\[(?<property>[_.0-9a-zA-Z\u00aa\u00b5\u00ba\u00c0-\u00d6\u00d8-\u00f6\u00f8-\u02b8\u02bb-\u02c1\u02d0-\u02d1\u02e0-\u02e4\u02ee\u0370-\u0373\u0376-\u0377\u037a-\u037d\u0386\u0388-\u038a\u038c\u038e-\u03a1\u03a3-\u03f5\u03f7-\u0481\u048a-\u0523\u0531-\u0556\u0559\u0561-\u0587\u05d0-\u05ea\u05f0-\u05f2\u0621-\u064a\u0660-\u0669\u066e-\u066f\u0671-\u06d3\u06d5\u06e5-\u06e6\u06ee-\u06fc\u06ff\u0710\u0712-\u072f\u074d-\u07a5\u07b1\u07c0-\u07ea\u07f4-\u07f5\u07fa\u0904-\u0939\u093d\u0950\u0958-\u0961\u0966-\u096f\u0971-\u0972\u097b-\u097f\u0985-\u098c\u098f-\u0990\u0993-\u09a8\u09aa-\u09b0\u09b2\u09b6-\u09b9\u09bd\u09ce\u09dc-\u09dd\u09df-\u09e1\u09e6-\u09f1\u0a05-\u0a0a\u0a0f-\u0a10\u0a13-\u0a28\u0a2a-\u0a30\u0a32-\u0a33\u0a35-\u0a36\u0a38-\u0a39\u0a59-\u0a5c\u0a5e\u0a66-\u0a6f\u0a72-\u0a74\u0a85-\u0a8d\u0a8f-\u0a91\u0a93-\u0aa8\u0aaa-\u0ab0\u0ab2-\u0ab3\u0ab5-\u0ab9\u0abd\u0ad0\u0ae0-\u0ae1\u0ae6-\u0aef\u0b05-\u0b0c\u0b0f-\u0b10\u0b13-\u0b28\u0b2a-\u0b30\u0b32-\u0b33\u0b35-\u0b39\u0b3d\u0b5c-\u0b5d\u0b5f-\u0b61\u0b66-\u0b6f\u0b71\u0b83\u0b85-\u0b8a\u0b8e-\u0b90\u0b92-\u0b95\u0b99-\u0b9a\u0b9c\u0b9e-\u0b9f\u0ba3-\u0ba4\u0ba8-\u0baa\u0bae-\u0bb9\u0bd0\u0be6-\u0bef\u0c05-\u0c0c\u0c0e-\u0c10\u0c12-\u0c28\u0c2a-\u0c33\u0c35-\u0c39\u0c3d\u0c58-\u0c59\u0c60-\u0c61\u0c66-\u0c6f\u0c85-\u0c8c\u0c8e-\u0c90\u0c92-\u0ca8\u0caa-\u0cb3\u0cb5-\u0cb9\u0cbd\u0cde\u0ce0-\u0ce1\u0ce6-\u0cef\u0d05-\u0d0c\u0d0e-\u0d10\u0d12-\u0d28\u0d2a-\u0d39\u0d3d\u0d60-\u0d61\u0d66-\u0d6f\u0d7a-\u0d7f\u0d85-\u0d96\u0d9a-\u0db1\u0db3-\u0dbb\u0dbd\u0dc0-\u0dc6\u0e01-\u0e30\u0e32-\u0e33\u0e40-\u0e46\u0e50-\u0e59\u0e81-\u0e82\u0e84\u0e87-\u0e88\u0e8a\u0e8d\u0e94-\u0e97\u0e99-\u0e9f\u0ea1-\u0ea3\u0ea5\u0ea7\u0eaa-\u0eab\u0ead-\u0eb0\u0eb2-\u0eb3\u0ebd\u0ec0-\u0ec4\u0ec6\u0ed0-\u0ed9\u0edc-\u0edd\u0f00\u0f20-\u0f29\u0f40-\u0f47\u0f49-\u0f6c\u0f88-\u0f8b\u1000-\u102a\u103f-\u1049\u1050-\u1055\u105a-\u105d\u1061\u1065-\u1066\u106e-\u1070\u1075-\u1081\u108e\u1090-\u1099\u10a0-\u10c5\u10d0-\u10fa\u10fc\u1100-\u1159\u115f-\u11a2\u11a8-\u11f9\u1200-\u1248\u124a-\u124d\u1250-\u1256\u1258\u125a-\u125d\u1260-\u1288\u128a-\u128d\u1290-\u12b0\u12b2-\u12b5\u12b8-\u12be\u12c0\u12c2-\u12c5\u12c8-\u12d6\u12d8-\u1310\u1312-\u1315\u1318-\u135a\u1380-\u138f\u13a0-\u13f4\u1401-\u166c\u166f-\u1676\u1681-\u169a\u16a0-\u16ea\u1700-\u170c\u170e-\u1711\u1720-\u1731\u1740-\u1751\u1760-\u176c\u176e-\u1770\u1780-\u17b3\u17d7\u17dc\u17e0-\u17e9\u1810-\u1819\u1820-\u1877\u1880-\u18a8\u18aa\u1900-\u191c\u1946-\u196d\u1970-\u1974\u1980-\u19a9\u19c1-\u19c7\u19d0-\u19d9\u1a00-\u1a16\u1b05-\u1b33\u1b45-\u1b4b\u1b50-\u1b59\u1b83-\u1ba0\u1bae-\u1bb9\u1c00-\u1c23\u1c40-\u1c49\u1c4d-\u1c7d\u1d00-\u1dbf\u1e00-\u1f15\u1f18-\u1f1d\u1f20-\u1f45\u1f48-\u1f4d\u1f50-\u1f57\u1f59\u1f5b\u1f5d\u1f5f-\u1f7d\u1f80-\u1fb4\u1fb6-\u1fbc\u1fbe\u1fc2-\u1fc4\u1fc6-\u1fcc\u1fd0-\u1fd3\u1fd6-\u1fdb\u1fe0-\u1fec\u1ff2-\u1ff4\u1ff6-\u1ffc\u2071\u207f\u2090-\u2094\u2102\u2107\u210a-\u2113\u2115\u2119-\u211d\u2124\u2126\u2128\u212a-\u212d\u212f-\u2139\u213c-\u213f\u2145-\u2149\u214e\u2183-\u2184\u2c00-\u2c2e\u2c30-\u2c5e\u2c60-\u2c6f\u2c71-\u2c7d\u2c80-\u2ce4\u2d00-\u2d25\u2d30-\u2d65\u2d6f\u2d80-\u2d96\u2da0-\u2da6\u2da8-\u2dae\u2db0-\u2db6\u2db8-\u2dbe\u2dc0-\u2dc6\u2dc8-\u2dce\u2dd0-\u2dd6\u2dd8-\u2dde\u3005-\u3006\u3031-\u3035\u303b-\u303c\u3041-\u3096\u309d-\u309f\u30a1-\u30fa\u30fc-\u30ff\u3105-\u312d\u3131-\u318e\u31a0-\u31b7\u31f0-\u31ff\u3400-\u4db5\u4e00-\u9fc3\ua000-\ua48c\ua500-\ua60c\ua610-\ua62b\ua640-\ua65f\ua662-\ua66e\ua680-\ua697\ua722-\ua788\ua78b-\ua78c\ua7fb-\ua801\ua803-\ua805\ua807-\ua80a\ua80c-\ua822\ua840-\ua873\ua882-\ua8b3\ua8d0-\ua8d9\ua900-\ua925\ua930-\ua946\uaa00-\uaa28\uaa40-\uaa42\uaa44-\uaa4b\uaa50-\uaa59\uac00-\ud7a3\uf900-\ufa2d\ufa30-\ufa6a\ufa70-\ufad9\ufb00-\ufb06\ufb13-\ufb17\ufb1d\ufb1f-\ufb28\ufb2a-\ufb36\ufb38-\ufb3c\ufb3e\ufb40-\ufb41\ufb43-\ufb44\ufb46-\ufbb1\ufbd3-\ufd3d\ufd50-\ufd8f\ufd92-\ufdc7\ufdf0-\ufdfb\ufe70-\ufe74\ufe76-\ufefc\uff10-\uff19\uff21-\uff3a\uff41-\uff5a\uff66-\uffbe\uffc2-\uffc7\uffca-\uffcf\uffd2-\uffd7\uffda-\uffdc]+)(?:\:(?<format>.+?))?(?<!\\)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		Dictionary<Type, object> customEvents = new Dictionary<Type, object>();
		Dictionary<Type, object> transactedCustomEvents = new Dictionary<Type, object>();
		Dictionary<string, FormatToken[]> formats = new Dictionary<string, FormatToken[]>();
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

		public virtual bool IsCachable
		{
			get
			{
				return Provider != null && Provider.IsCachable;
			}
		}

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
		/// Attempt to determine whether a type is a "multi-type"
		/// (enumerable/list), and if so, also determine its item type.
		/// </summary>
		internal static bool TryGetMultiType(Type type, out bool isCollection, out Type itemType)
		{
			if (type == typeof(string))
			{
				isCollection = false;
				itemType = null;
				return false;
			}

			if (type.IsGenericType)
			{
				var genericTypeDef = type.GetGenericTypeDefinition();

				// Shortcuts for expression result of type ICollection<T>, IList<T>, List<T>.
				// NOTE: IsAssignableFrom would work as long as a generic type parameter, e.g. Object, was specified.
				if (genericTypeDef == typeof(ICollection<>) || genericTypeDef == typeof(IList<>) || genericTypeDef == typeof(List<>))
				{
					isCollection = true;
					itemType = type.GetGenericArguments()[0];
					return true;
				}

				// Shortcuts for expression result of type IEnumerable<T>
				if (genericTypeDef == typeof(IEnumerable<>))
				{
					isCollection = false;
					itemType = type.GetGenericArguments()[0];
					return true;
				}
			}

			Type enumerableItemType = null;

			// Look for implementation of IEnumerable<T>.
			foreach (var i in type.GetInterfaces())
			{
				if (i.IsGenericType)
				{
					var genericTypeDef = i.GetGenericTypeDefinition();

					if (genericTypeDef == typeof(ICollection<>))
					{
						isCollection = true;
						itemType = i.GetGenericArguments()[0];
						return true;
					}

					if (genericTypeDef == typeof(IEnumerable<>))
						enumerableItemType = i.GetGenericArguments()[0];
				}
			}

			if (enumerableItemType != null)
			{
				isCollection = false;
				itemType = enumerableItemType;
				return true;
			}

			isCollection = false;
			itemType = null;
			return false;
		}

		/// <summary>
		/// Gets the item type of a list type, or returns false if the type is not a supported list type.
		/// </summary>
		protected internal virtual bool TryGetListItemType(Type listType, out Type itemType)
		{
			bool isCollection;
			if (TryGetMultiType(listType, out isCollection, out itemType) && isCollection)
				return true;

			itemType = null;
			return false;
		}

		/// <summary>
		/// Get basic information about the given type.
		/// </summary>
		internal static void GetTypeInfo(Type type, out bool isModelType, out bool isMulti, out Type itemType)
		{
			if (typeof(IModelInstance).IsAssignableFrom(type))
			{
				isModelType = true;
				isMulti = false;
				itemType = null;
			}
			else
			{
				bool isCollection;
				if (TryGetMultiType(type, out isCollection, out itemType))
				{
					isMulti = true;
					isModelType = typeof(IModelInstance).IsAssignableFrom(itemType);
				}
				else
				{
					isMulti = false;
					isModelType = false;
				}
			}
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
			if (BaseType != null && IsCachable)
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
			if (string.IsNullOrWhiteSpace(expression))
				return null;

			var key = expression;
			if (resultType != null)
			{
				var typeName = resultType.Name;
				if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Nullable<>))
					typeName = Nullable.GetUnderlyingType(resultType).Name;

				key = expression + "|" + typeName;
			}

			ModelExpression exp;
			if (!Expressions.TryGetValue(key, out exp))
				Expressions[key] = exp = new ModelExpression(this, expression, resultType, querySyntax);

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
				return modelExpression != null ? true : false;
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
		/// <param name="property">The name of the property</param>
		/// <returns>The formatted of the property</returns>
		public string GetFormattedValue(string property, string format, IFormatProvider provider)
		{
			return GetFormattedValue(Properties[property], format, provider);
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
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelProperty"/></param>
		/// <returns>The formatted value of the property</returns>
		public string GetFormattedValue(ModelProperty property, string format, IFormatProvider provider)
		{
			return property.GetFormattedValue(null, format, provider);
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
				return BooleanFormatInfo.Instance;
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
		/// Returns a list of <see cref="ModelSource"/> given a format string
		/// </summary>
		/// <param name="format">Format string from which the model sources should be extracted</param>
		/// <returns>List of <see cref="ModelSource"/>s</returns>
		public ModelSource[] GetFormatModelSources(string format)
		{
			FormatToken[] formatTokens;
			TryGetFormatTokens(format, out formatTokens);
			return formatTokens.Select(ft => ft.Property).ToArray();
		}

		/// <summary>
		/// Attempts to format the instance using the specified format.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		internal bool TryFormatInstance(ModelInstance instance, string format, out string value)
		{
			FormatToken[] formatTokens;
			bool isValid = TryGetFormatTokens(format, out formatTokens);

			// Handle simple case of [Property]
			if (formatTokens.Length == 1 && formatTokens[0].Literal == null)
			{
				try
				{
					value = formatTokens[0].Property.GetFormattedValue(instance, formatTokens[0].Format);
					return true;
				}
				catch
				{
					value = "";
					return false;
				}
			}

			// Use a string builder to create and return the formatted result
			StringBuilder result = new StringBuilder();
			foreach (var token in formatTokens)
			{
				result.Append(token.Literal);
				if (token.Property != null)
				{
					try
					{
						result.Append(token.Property.GetFormattedValue(instance, token.Format));
					}
					catch
					{
						isValid = false;
					}
				}
			}

			value = result.ToString();

			return isValid;
		}

		/// <summary>
		/// Attempts to get the tokens contained in the given format string.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		public bool TryGetFormatTokens(string format, out FormatToken[] formatTokens)
		{
			bool hasError = false;

			// Get the list of format tokens by parsing the format expression
			if (!formats.TryGetValue(format, out formatTokens))
			{
				// Replace \\ escape sequence with char 0, \[ escape sequence with char 1, and \] escape sequence with char 2
				var escapedFormat = format.Replace(@"\\", ((char)0).ToString(CultureInfo.InvariantCulture)).Replace(@"\[", ((char)1).ToString()).Replace(@"\]", ((char)1).ToString());

				// Replace \\ escape sequence with \, \[ escape sequence with [, and \] escape sequence with ]
				var correctedFormat = format.Replace(@"\\", @"\").Replace(@"\[", "[").Replace(@"\]", "]");

				var formatTokensList = new List<FormatToken>();
				int index = 0;
				foreach (Match substitution in FormatParser.Matches(escapedFormat))
				{
					var path = substitution.Groups["property"].Value;
					ModelPath modelPath;
					if (!TryGetPath(path, out modelPath))
					{
						hasError = true;
						formatTokensList.Add(new FormatToken(index, substitution.Index + substitution.Length - 1)
						{
							Literal = correctedFormat.Substring(index, substitution.Index - index),
						});
					}
					else
					{
						formatTokensList.Add(new FormatToken(index, substitution.Index + substitution.Length - 1)
						{
							Literal = substitution.Index > index ? correctedFormat.Substring(index, substitution.Index - index) : null,
							Property = new ModelSource(modelPath),
							Format = substitution.Groups["format"].Success ? correctedFormat.Substring(substitution.Groups["format"].Index, substitution.Groups["format"].Length) : null,
						});
					}

					index = substitution.Index + substitution.Length;
				}

				// Add the trailing literal
				if (index < correctedFormat.Length)
				{
					formatTokensList.Add(new FormatToken(index, correctedFormat.Length - 1)
					{
						Literal = correctedFormat.Substring(index, correctedFormat.Length - index),
					});
				}

				formatTokens = formatTokensList.ToArray();

				// Cache the parsed format expression
				if (!hasError)
					formats.Add(format, formatTokens);
			}

			return !hasError;
		}

		/// <summary>
		/// Adds model steps to the specified root step based on the specified instance format
		/// </summary>
		/// <param name="format"></param>
		/// <param name="rootStep"></param>
		internal void AddFormatSteps(ModelStep rootStep, string format)
		{
			FormatToken[] formatTokens;
			TryGetFormatTokens(format, out formatTokens);
			foreach (var token in formatTokens)
			{
				ModelStep modelStep = rootStep;
				foreach (var sourceStep in token.Property.Steps)
				{
					modelStep = new ModelStep(rootStep.Path) { PreviousStep = modelStep, Property = sourceStep.DeclaringType.Properties[sourceStep.Property] };
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
		public class FormatToken
		{
			public FormatToken(int startIndex, int endIndex)
			{
				StartIndex = startIndex;
				EndIndex = endIndex;
			}

			public int StartIndex { get; set; }

			public int EndIndex { get; set; }

			public string Literal { get; internal set; }

			public ModelSource Property { get; internal set; }

			public string Format { get; internal set; }

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
		internal class BooleanFormatInfo : IFormatProvider, ICustomFormatter
		{
			internal static BooleanFormatInfo Instance = new BooleanFormatInfo();

			private BooleanFormatInfo()
			{
			}

			object IFormatProvider.GetFormat(Type formatType)
			{
				if (formatType == typeof(ICustomFormatter))
					return this;
				return null;
			}

			string ICustomFormatter.Format(string format, object arg, IFormatProvider formatProvider)
			{
				if (arg is Boolean)
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
				else if (arg is IFormattable)
					return ((IFormattable)arg).ToString(format, formatProvider);
				else if (arg == null)
					return "";
				else
					return arg.ToString();
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
