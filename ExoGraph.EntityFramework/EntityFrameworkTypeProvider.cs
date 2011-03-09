using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Objects;
using System.Data;
using System.Data.Objects.DataClasses;
using System.ComponentModel;
using System.Data.Metadata.Edm;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.Collections;
using System.Reflection;

namespace ExoGraph.EntityFramework
{
	public class EntityFrameworkGraphTypeProvider : ReflectionGraphTypeProvider
	{
		public EntityFrameworkGraphTypeProvider(Func<GraphObjectContext> createContext)
			: this("", createContext)
		{ }

		public EntityFrameworkGraphTypeProvider(string @namespace, Func<GraphObjectContext> createContext)
			: base(@namespace, GetEntityTypes(createContext()), null) 
		{
			this.CreateContext = createContext;
		}

		Func<GraphObjectContext> CreateContext { get; set; }

		static IEnumerable<Type> GetEntityTypes(GraphObjectContext context)
		{
			using (context)
			{
				foreach (Type type in context.GetType().Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GraphEntity))))
					yield return type;
			}
		}

		internal GraphObjectContext GetObjectContext()
		{
			Storage storage = GetStorage();

			if (storage.Context == null)
			{
				storage.Context = CreateContext();
				storage.Context.MetadataWorkspace.LoadFromAssembly(storage.Context.GetType().Assembly);

				// Raise OnSave whenever the object context is committed
				storage.Context.SavedChanges += (sender, e) =>
				{
					var context = sender as GraphObjectContext;
					var firstEntity = context.ObjectStateManager.GetObjectStateEntries(EntityState.Added | EntityState.Deleted | EntityState.Modified | EntityState.Unchanged).Where(p => p.Entity != null).Select(p => p.Entity).FirstOrDefault();

					// Raise the save event on the first found dirty entity
					if (firstEntity != null)
					{
						var graphInstance = GraphContext.Current.GetGraphInstance(firstEntity);

						if (graphInstance != null)
							((EntityFrameworkGraphTypeProvider.EntityGraphType) graphInstance.Type).RaiseOnSave(graphInstance);
					}
				};
			}

			return storage.Context;
		}

		[ThreadStatic]
		static Storage context;

		/// <summary>
		/// Gets thread static or <see cref="HttpContext"/> storage for the <see cref="GraphContext"/>.
		/// </summary>
		/// <returns></returns>
		static Storage GetStorage()
		{
			HttpContext webCtx = HttpContext.Current;

			// If in a web request, store the reference in HttpContext
			if (webCtx != null)
			{
				Storage storage = (Storage)webCtx.Items[typeof(EntityFrameworkGraphTypeProvider)];

				if (storage == null)
					webCtx.Items[typeof(EntityFrameworkGraphTypeProvider)] = storage = new Storage();

				return storage;
			}

			// Otherwise, store the reference in a thread static variable
			else
			{
				if (context == null)
					context = new Storage();

				return context;
			}
		}

		protected override ReflectionGraphType CreateGraphType(string @namespace, Type type)
		{
			return new EntityGraphType(@namespace, type, "");
		}

		internal static GraphInstance CreateGraphInstance(object instance)
		{
			GraphInstance graphInstance = ReflectionGraphTypeProvider.CreateGraphInstance(instance);

			return graphInstance;
		}

		/// <summary>
		/// Fetches any attributes on matching properties in an entity's "buddy class"
		/// </summary>
		/// <param name="declaringType"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		private Attribute[] GetBuddyClassAttributes(GraphType declaringType, System.Reflection.PropertyInfo property)
		{
			Attribute[] attributes = null;

			var entityGraphType = declaringType as EntityGraphType;
			if (entityGraphType.BuddyClass != null)
			{
				var buddyClassProperty = entityGraphType.BuddyClass.GetProperty(property.Name);

				if (buddyClassProperty != null)
					attributes = buddyClassProperty.GetCustomAttributes(true).Cast<Attribute>().ToArray();
			}

			return attributes ?? new Attribute[0];
		}

		/// <summary>
		/// Overridden to allow the addition of buddy-class attributes to the list of attributes associated with the <see cref="GraphType"/>
		/// </summary>
		/// <param name="declaringType"></param>
		/// <param name="property"></param>
		/// <param name="name"></param>
		/// <param name="isStatic"></param>
		/// <param name="isBoundary"></param>
		/// <param name="propertyType"></param>
		/// <param name="isList"></param>
		/// <param name="attributes"></param>
		/// <returns></returns>
		protected override GraphReferenceProperty CreateReferenceProperty(GraphType declaringType, System.Reflection.PropertyInfo property, string name, bool isStatic, GraphType propertyType, bool isList, Attribute[] attributes)
		{
			// Fetch any attributes associated with a buddy-class
			attributes = attributes.Union(GetBuddyClassAttributes(declaringType, property)).ToArray();

			return base.CreateReferenceProperty(declaringType, property, name, isStatic, propertyType, isList, attributes);
		}

		/// <summary>
		/// Overridden to allow the addition of buddy-class attributes to the list of attributes associated with the <see cref="GraphType"/>
		/// </summary>
		/// <param name="declaringType"></param>
		/// <param name="property"></param>
		/// <param name="name"></param>
		/// <param name="isStatic"></param>
		/// <param name="isBoundary"></param>
		/// <param name="propertyType"></param>
		/// <param name="isList"></param>
		/// <param name="attributes"></param>
		/// <returns></returns>
		protected override GraphValueProperty CreateValueProperty(GraphType declaringType, System.Reflection.PropertyInfo property, string name, bool isStatic, Type propertyType, TypeConverter converter, bool isList, Attribute[] attributes)
		{
			// Do not include entity reference properties in the model
			if (property.PropertyType.IsSubclassOf(typeof(EntityReference)))
			    return null;

			// Fetch any attributes associated with a buddy-class
			attributes = attributes.Union(GetBuddyClassAttributes(declaringType, property)).ToArray();

			return base.CreateValueProperty(declaringType, property, name, isStatic, propertyType, converter, isList, attributes);
		}

		#region Storage

		/// <summary>
		/// Reference class used to provide storage for the context.
		/// </summary>
		class Storage
		{
			public GraphObjectContext Context { get; set; }
		}

		#endregion

		#region EntityGraphType

		[Serializable]
		public class EntityGraphType : ReflectionGraphType
		{
			string @namespace;
			string qualifiedEntitySetName;
			PropertyInfo[] idProperties;

			protected internal EntityGraphType(string @namespace, Type type, string scope)
				: base(@namespace, type, scope)
			{
				this.@namespace = @namespace;
			}

			/// <summary>
			/// Gets an array of <see cref="PropertyInfo"/> instances for each property marked 
			/// as being an entity key for the current type.
			/// </summary>
			PropertyInfo[] IdProperties
			{
				get
				{
					if (idProperties == null)
					{
						idProperties = 
							base.GetEligibleProperties()
							.Where(p => 
								p.GetCustomAttributes(typeof(EdmScalarPropertyAttribute), true)
								.Cast<EdmScalarPropertyAttribute>().Where(a => a.EntityKeyProperty).Any()
							)
							.ToArray();
					}
					return idProperties;
				}
			}

			/// <summary>
			/// Override to ensure that entity key properties are excluded from the model.
			/// </summary>
			/// <returns></returns>
			protected override IEnumerable<PropertyInfo> GetEligibleProperties()
			{
				return base.GetEligibleProperties().Where(p => !IdProperties.Contains(p));
			}

			/// <summary>
			/// Performs initialization of the graph type outside of the constructor to avoid recursion deadlocks.
			/// </summary>
			protected override void OnInit()
			{
				// Fetch the "buddy class", if any
				var metadataTypeAttribute = this.UnderlyingType.GetCustomAttributes(typeof(MetadataTypeAttribute), false).OfType<MetadataTypeAttribute>().FirstOrDefault();			
				if (metadataTypeAttribute != null)
					BuddyClass = metadataTypeAttribute.MetadataClassType;

				base.OnInit();

				// Get the current object context
				GraphObjectContext context = GetObjectContext();

				// Find the base entity graph type
				GraphType baseType = this;
				while (baseType.BaseType is EntityGraphType)
					baseType = baseType.BaseType;

				string entityNamespace = context.GetType().Namespace;

				// Determine the qualified entity set name
				// This assumes:
				//   1. only one entity container
				//   2. only one entity set for an given entity type
				//   3. only one entity type with a name that matches the graph type
				qualifiedEntitySetName = context.DefaultContainerName + "." +
					context.MetadataWorkspace.GetItems<EntityContainer>(DataSpace.CSpace)[0]
						.BaseEntitySets.First(s => s.ElementType.Name == ((EntityFrameworkGraphTypeProvider.EntityGraphType)baseType).UnderlyingType.Name).Name;

				// Get the entity type of the current graph type
				var entityType = context.MetadataWorkspace.GetItem<EntityType>(entityNamespace + "." + UnderlyingType.Name, DataSpace.OSpace);

				// Find all back-references from entities contained in each parent-child relationship for use when
				// items are expected to be deleted because they were removed from the assocation
				var listAssociations = new Dictionary<GraphReferenceProperty, GraphReferenceProperty>();
				foreach (GraphReferenceProperty property in Properties.Where(p => p.IsList && p is GraphReferenceProperty))
				{
					var relatedGraphType = ((GraphReferenceProperty) property).PropertyType;
					var relatedEntityType = context.MetadataWorkspace.GetItem<EntityType>(entityNamespace + "." + ((EntityFrameworkGraphTypeProvider.EntityGraphType)relatedGraphType).UnderlyingType.Name, DataSpace.OSpace);
					NavigationProperty manyNavProp;
					if (!entityType.NavigationProperties.TryGetValue(property.Name, false, out manyNavProp))
						continue;
					var oneNavProp = relatedEntityType.NavigationProperties.FirstOrDefault(np => np.RelationshipType == manyNavProp.RelationshipType);
					if (oneNavProp == null)
						continue;

					var oneNavDeclaringType = GraphContext.Current.GetGraphType(@namespace + oneNavProp.DeclaringType.Name);
					oneNavDeclaringType.AfterInitialize(delegate
					{
						var parentReference = property.PropertyType.Properties[oneNavProp.Name];
						if (parentReference != null && parentReference.HasAttribute<RequiredAttribute>())
							listAssociations[property] = parentReference as GraphReferenceProperty;
					});
				}

				// When a list-change event is raised, check to see if an parent/child dependency exists, and delete the child if
				// the parent is deleted
				this.ListChange += (sender, e) =>
				{
					if (e.Removed.Any())
					{
						if (listAssociations[e.Property] != null)
						{
							GraphInstance removed = e.Removed.First();

							if (removed.GetReference(listAssociations[e.Property]) == null)
								removed.Delete();
						}
						return;
					}
				};
			}

			internal Type BuddyClass { get; set; }

			protected override System.Collections.IList ConvertToList(GraphReferenceProperty property, object list)
			{
				// If the list is managed by Entity Framework, convert to a list with listeners
				if (list is RelatedEnd)
				{
					Type d1 = typeof(CollectionWrapper<>);
					Type constructed = d1.MakeGenericType(((EntityGraphType) property.PropertyType).UnderlyingType);

					var constructor = constructed.GetConstructors()[0];
					return (IList) constructor.Invoke(new object[] { list });
				}

				return base.ConvertToList(property, list);
			}

			/// <summary>
			/// Gets or creates the object context for the current scope of work that corresponds to the 
			/// current <see cref="EntityGraphType"/>.
			/// </summary>
			/// <returns></returns>
			internal GraphObjectContext GetObjectContext()
			{
				return ((EntityFrameworkGraphTypeProvider)Provider).GetObjectContext();
			}

			protected override void SaveInstance(GraphInstance graphInstance)
			{
				GetObjectContext().SaveChanges(SaveOptions.AcceptAllChangesAfterSave);
			}

			protected override string GetId(object instance)
			{
				return ((GraphEntity)instance).Id;
			}

			internal void OnPropertyGet(GraphInstance instance, string property)
			{
				base.OnPropertyGet(instance, property);
			}

			internal void OnPropertyChanged(GraphInstance instance, string property, object oldValue, object newValue)
			{
				base.OnPropertyChanged(instance, property, oldValue, newValue);
			}

			public override GraphInstance GetGraphInstance(object instance)
			{
				return ((GraphEntity)instance).Instance;
			}

			protected override object GetInstance(string id)
			{
				// Get the current object context
				GraphObjectContext context = GetObjectContext();

				// Create a new instance if the id is null
				if (id == null)
				{
					// When a new entity is created, it is detached by default.  Attach it to the context so it will be tracked.
					var entity = context.GetType().GetMethod("CreateObject").MakeGenericMethod(UnderlyingType).Invoke(context, null);
					context.AddObject(qualifiedEntitySetName, entity);

					return entity;
				}

				// Otherwise, load the existing instance

				// Split the id string into id tokens
				var tokens = id.Split(',');
				if (tokens.Length != idProperties.Length)
					throw new ArgumentException("The specified id, '" + id + "', does not have the correct number of key values.");

				// Create an entity key based on the specified id tokens
				var key = new EntityKey(qualifiedEntitySetName,
					idProperties.Select((property, index) => new EntityKeyMember(property.Name, TypeDescriptor.GetConverter(property.PropertyType).ConvertFromString(tokens[index]))));

				// Attempt to create the entity using the key
				object instance;
				context.TryGetObjectByKey(key, out instance);
				return instance as GraphEntity;
			}

			protected override void DeleteInstance(GraphInstance graphInstance)
			{
				GetObjectContext().DeleteObject(graphInstance.Instance);
			}

			internal void RaiseOnSave(GraphInstance instance)
			{
				base.OnSave(instance);
			}
		}
		#endregion
	}
}
