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
	[Serializable]
	public class GraphType : ISerializable
	{
		#region Fields

		Dictionary<Type, object> customEvents = new Dictionary<Type, object>();
		Dictionary<Type, object> transactedCustomEvents = new Dictionary<Type, object>();
		Attribute[] attributes;
		int nextPropertyIndex;

		#endregion

		#region Contructors

		internal GraphType(GraphContext context, string name, string qualifiedName, Attribute[] attributes, Func<GraphInstance, object> extensionFactory)
		{
			this.Context = context;
			this.Name = name;
			this.QualifiedName = qualifiedName;
			this.attributes = attributes;
			this.ExtensionFactory = extensionFactory;

			// Initialize list properties
			this.SubTypes = new GraphTypeList();
			this.Properties = new GraphPropertyList();
			this.InReferences = new List<GraphReferenceProperty>();
			this.OutReferences = new GraphReferencePropertyList();
			this.Values = new GraphValuePropertyList();
			this.Paths = new GraphPathList();

			// Register the new type with the graph context
			context.RegisterGraphType(this);
		}

		#endregion

		#region Properties

		public GraphContext Context { get; private set;  }

		public string Name { get; private set; }

		public string QualifiedName { get; private set; }

		public GraphType BaseType { get; private set; }

		public GraphTypeList SubTypes { get; private set; }

		public GraphPropertyList Properties { get; private set; }

		internal Func<GraphInstance, object> ExtensionFactory { get; private set; }

		internal IEnumerable<GraphReferenceProperty> InReferences { get; private set; }

		internal GraphReferencePropertyList OutReferences { get; private set; }

		internal GraphValuePropertyList Values { get; private set; }

		internal GraphPathList Paths { get; private set; }

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

		/// <summary>
		/// Creates a new instance of the current <see cref="GraphType"/>.
		/// </summary>
		/// <returns></returns>
		public GraphInstance Create()
		{
			return Context.GetGraphInstance(Context.GetInstance(this, null));
		}

		/// <summary>
		/// Creates an existing instance of the current <see cref="GraphType"/>.
		/// </summary>
		/// <returns></returns>
		public GraphInstance Create(string id)
		{
			object instance = Context.GetInstance(this, id);
			return instance == null ? null : Context.GetGraphInstance(instance);
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
				return Context.GetGraphInstance(reference);
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
		/// Adds the specified property to the current graph type.
		/// </summary>
		/// <param name="property"></param>
		internal void AddProperty(GraphProperty property)
		{
			if (property.DeclaringType == this)
				property.Index = nextPropertyIndex++;

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
		/// Sets the base <see cref="GraphType"/> for the current type and 
		/// adds the current type to the list of sub types for the base type.
		/// </summary>
		/// <param name="baseType"></param>
		internal void SetBaseType(GraphType baseType)
		{
			if (this.BaseType != null)
				throw new InvalidOperationException("The base type of a graph type cannot be changed once it has been set.");

			this.BaseType = baseType;
			baseType.SubTypes.Add(this);
			nextPropertyIndex = baseType.nextPropertyIndex - 1;
		}

		/// <summary>
		/// Creates an instance extension using the extension factory specified for the current <see cref="GraphType"/>.
		/// </summary>
		/// <returns></returns>
		internal object CreateExtension(GraphInstance instance)
		{
			if (ExtensionFactory == null)
				return null;
			else
				return ExtensionFactory(instance);
		}

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
				return GraphContext.Current.GetGraphType(typeName);
			}
			#endregion
		}
		#endregion
	}
}
