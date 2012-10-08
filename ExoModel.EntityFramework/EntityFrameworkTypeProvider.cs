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

namespace ExoModel.EntityFramework
{
	public class EntityFrameworkModelTypeProvider : ReflectionModelTypeProvider
	{
		static MethodInfo entityDeletedEvent = typeof(ObjectStateManager).GetEvent("EntityDeleted", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetAddMethod(true);

		public EntityFrameworkModelTypeProvider(Func<object> createContext)
			: this("", createContext)
		{ }

		public EntityFrameworkModelTypeProvider(string @namespace, Func<object> createContext)
			: base(@namespace, GetEntityTypes(createContext())) 
		{
			this.CreateContext = createContext;
			this.DefaultFormatProperties = new string[] { "Label", "Name", "Text", "FullName", "Title", "Description" };
		}

		Func<object> CreateContext { get; set; }

		static IEnumerable<Type> GetEntityTypes(object context)
		{
			foreach (Type type in context.GetType().Assembly.GetTypes().Where(t => typeof(IModelInstance).IsAssignableFrom(t)))
				yield return type;
		}

		internal IEntityContext GetObjectContext()
		{
			Storage storage = GetStorage();

			if (storage.Context == null)
			{
				storage.Context = CreateContext() as IEntityContext;
				storage.Context.ObjectContext.MetadataWorkspace.LoadFromAssembly(storage.Context.GetType().Assembly);

				// Raise OnSave whenever the object context is committed
				storage.Context.SavedChanges += (sender, e) =>
				{
					var context = sender as IEntityContext;
					var firstEntity = context.ObjectContext.ObjectStateManager.GetObjectStateEntries(EntityState.Added | EntityState.Deleted | EntityState.Modified | EntityState.Unchanged).Where(p => p.Entity != null).Select(p => p.Entity).FirstOrDefault();

					// Raise the save event on the first found dirty entity
					if (firstEntity != null)
					{
						var modelInstance = ModelContext.Current.GetModelInstance(firstEntity);

						if (modelInstance != null)
							((EntityFrameworkModelTypeProvider.EntityModelType) modelInstance.Type).RaiseOnSave(modelInstance);
					}
				};

				// Raise OnStateManagerChanged when an object is modified
				entityDeletedEvent.Invoke(storage.Context.ObjectContext.ObjectStateManager,
					new object[] { new CollectionChangeEventHandler((sender, e) =>
					{
						if (e.Action == CollectionChangeAction.Remove)
							return;
					}) });
			}

			return storage.Context;
		}

		[ThreadStatic]
		static Storage context;

		/// <summary>
		/// Gets thread static or <see cref="HttpContext"/> storage for the <see cref="ModelContext"/>.
		/// </summary>
		/// <returns></returns>
		static Storage GetStorage()
		{
			HttpContext webCtx = HttpContext.Current;

			// If in a web request, store the reference in HttpContext
			if (webCtx != null)
			{
				Storage storage = (Storage)webCtx.Items[typeof(EntityFrameworkModelTypeProvider)];

				if (storage == null)
					webCtx.Items[typeof(EntityFrameworkModelTypeProvider)] = storage = new Storage();

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

		protected override Type GetUnderlyingType(object instance)
		{
			var type = instance.GetType();
			if (type.Assembly.IsDynamic)
				return type.BaseType;
			else
				return type;
		}

		protected override ReflectionModelType CreateModelType(string @namespace, Type type, string format)
		{
			return new EntityModelType(@namespace, type, "", format);
		}

		/// <summary>
		/// Fetches any attributes on matching properties in an entity's "buddy class"
		/// </summary>
		/// <param name="declaringType"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		private Attribute[] GetBuddyClassAttributes(ModelType declaringType, System.Reflection.PropertyInfo property)
		{
			Attribute[] attributes = null;

			var entityModelType = declaringType as EntityModelType;
			if (entityModelType.BuddyClass != null)
			{
				var buddyClassProperty = entityModelType.BuddyClass.GetProperty(property.Name);

				if (buddyClassProperty != null)
					attributes = buddyClassProperty.GetCustomAttributes(true).Cast<Attribute>().ToArray();
			}

			return attributes ?? new Attribute[0];
		}

		/// <summary>
		/// Overridden to allow the addition of buddy-class attributes to the list of attributes associated with the <see cref="ModelType"/>
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
		protected override ModelReferenceProperty CreateReferenceProperty(ModelType declaringType, System.Reflection.PropertyInfo property, string name, string label, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
		{
			// Fetch any attributes associated with a buddy-class
			attributes = attributes.Union(GetBuddyClassAttributes(declaringType, property)).ToArray();

			// Mark properties that are not mapped as not persisted
			isPersisted = !attributes.OfType<NotMappedAttribute>().Any();

			// Determine whether the property represents an actual entity framework navigation property or an custom property
			var context = GetObjectContext();
			var type = context.ObjectContext.MetadataWorkspace.GetItem<EntityType>(((EntityModelType)declaringType).UnderlyingType.FullName, DataSpace.CSpace);
			NavigationProperty navProp;
			type.NavigationProperties.TryGetValue(name, false, out navProp);
			return new EntityReferenceProperty(declaringType, navProp, property, name, label, format, isStatic, propertyType, isList, isReadOnly, isPersisted, attributes);
		}

		/// <summary>
		/// Overridden to allow the addition of buddy-class attributes to the list of attributes associated with the <see cref="ModelType"/>
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
		protected override ModelValueProperty CreateValueProperty(ModelType declaringType, System.Reflection.PropertyInfo property, string name, string label, string format, bool isStatic, Type propertyType, TypeConverter converter, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
		{
			// Do not include entity reference properties in the model
			if (property.PropertyType.IsSubclassOf(typeof(EntityReference)))
			    return null;

			// Fetch any attributes associated with a buddy-class
			attributes = attributes.Union(GetBuddyClassAttributes(declaringType, property)).ToArray();

			// Mark properties that are not mapped as not persisted
			isPersisted = !attributes.OfType<NotMappedAttribute>().Any();

			return new EntityValueProperty(declaringType, property, name, label, format, isStatic, propertyType, converter, isList, isReadOnly, isPersisted, attributes);
		}

		#region Storage

		/// <summary>
		/// Reference class used to provide storage for the context.
		/// </summary>
		class Storage
		{
			public IEntityContext Context { get; set; }
		}

		#endregion

		#region EntityModelType

		public class EntityModelType : ReflectionModelType
		{
			string @namespace;
			PropertyInfo[] idProperties;

			protected internal EntityModelType(string @namespace, Type type, string scope, string format)
				: base(@namespace, type, scope, format)
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
						var context = GetObjectContext();
						var type = context.ObjectContext.MetadataWorkspace.GetItem<EntityType>(UnderlyingType.FullName, DataSpace.CSpace);
						idProperties = type.KeyMembers.Select(m => GetProperty(UnderlyingType, m.Name)).ToArray();
					}
					return idProperties;
				}
			}

			PropertyInfo GetProperty(Type declaringType, string name)
			{
				if (declaringType == null)
					return null;
				var property = declaringType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				return property ?? GetProperty(declaringType.BaseType, name);
			}

			/// <summary>
			/// BuddyClass which represents associated entity metadata
			/// </summary>
			internal Type BuddyClass { get; set; }

			internal string QualifiedEntitySetName { get; private set; }

			/// <summary>
			/// List of properties which represent owners in owner/assigned relationships
			/// </summary>
			internal Dictionary<ModelReferenceProperty, ModelReferenceProperty> OwnerProperties { get; set; }

			/// <summary>
			/// Override to ensure that entity key properties are excluded from the model.
			/// </summary>
			/// <returns></returns>
			protected override IEnumerable<PropertyInfo> GetEligibleProperties()
			{
				return base.GetEligibleProperties().Where(p => !IdProperties.Contains(p));
			}

			/// <summary>
			/// Performs initialization of the model type outside of the constructor to avoid recursion deadlocks.
			/// </summary>
			protected override void OnInit()
			{
				// Fetch the "buddy class", if any
				var metadataTypeAttribute = this.UnderlyingType.GetCustomAttributes(typeof(MetadataTypeAttribute), false).OfType<MetadataTypeAttribute>().FirstOrDefault();			
				if (metadataTypeAttribute != null)
					BuddyClass = metadataTypeAttribute.MetadataClassType;

				base.OnInit();

				// Get the current object context
				IEntityContext context = GetObjectContext();

				// Find the base entity model type
				ModelType baseType = this;
				while (baseType.BaseType is EntityModelType)
					baseType = baseType.BaseType;

				string entityNamespace = context.GetType().Namespace;

				// Determine the qualified entity set name
				// This assumes:
				//   1. only one entity container
				//   2. only one entity set for an given entity type
				//   3. only one entity type with a name that matches the model type
				QualifiedEntitySetName = context.ObjectContext.DefaultContainerName + "." +
					context.ObjectContext.MetadataWorkspace.GetItems<EntityContainer>(DataSpace.CSpace)[0]
						.BaseEntitySets.First(s => s.ElementType.Name == ((EntityFrameworkModelTypeProvider.EntityModelType)baseType).UnderlyingType.Name).Name;

				// Get the entity type of the current model type
				var entityType = context.ObjectContext.MetadataWorkspace.GetItem<EntityType>(entityNamespace + "." + UnderlyingType.Name, DataSpace.OSpace);

				// Find all back-references from entities contained in each parent-child relationship for use when
				// items are expected to be deleted because they were removed from the assocation
				OwnerProperties = new Dictionary<ModelReferenceProperty, ModelReferenceProperty>();
				foreach (var property in Properties.OfType<EntityReferenceProperty>().Where(p => p.IsList))
				{
					if (!(property.PropertyType is EntityModelType))
						continue;

					var relatedEntityType = context.ObjectContext.MetadataWorkspace.GetItem<EntityType>(entityNamespace + "." + ((EntityModelType)property.PropertyType).UnderlyingType.Name, DataSpace.OSpace);
					NavigationProperty manyNavProp;
					if (!entityType.NavigationProperties.TryGetValue(property.Name, false, out manyNavProp))
						continue;
					var oneNavProp = relatedEntityType.NavigationProperties.FirstOrDefault(np => np.RelationshipType == manyNavProp.RelationshipType);
					if (oneNavProp == null)
						continue;

					var oneNavDeclaringType = ModelContext.Current.GetModelType(@namespace + oneNavProp.DeclaringType.Name);
					DeferOwnerDetection(oneNavDeclaringType, property, oneNavProp);
				}

				// When a list-change event is raised, check to see if an parent/child dependency exists, and delete the child if
				// the parent is deleted
				this.ListChange += (sender, e) =>
				{
					if (e.Removed.Any())
					{
						var propertyType = (EntityModelType) e.Property.PropertyType;
						ModelReferenceProperty relatedProperty;
						if (propertyType.OwnerProperties.TryGetValue(e.Property, out relatedProperty))
						{
							foreach (var instance in e.Removed)
							{
								if (instance.GetReference(relatedProperty) == null)
									instance.IsPendingDelete = true;
							}
						}
						return;
					}
				};

				// Automatically add new IModelEntity instances to the object context during initialization
				if (typeof(IModelEntity).IsAssignableFrom(UnderlyingType))
				{
					this.Init += (sender, e) =>
					{
						var entity = e.Instance.Instance as IModelEntity;
						if (!entity.IsInitialized && entity.EntityKey == null)
						{
							entity.IsInitialized = true;
							GetObjectContext().ObjectContext.AddObject(QualifiedEntitySetName, entity);
						}
					};
				}
			}

			private void DeferOwnerDetection(ModelType type, EntityReferenceProperty property, NavigationProperty oneNavProp)
			{
				type.AfterInitialize(delegate
				{
					var parentReference = property.PropertyType.Properties[oneNavProp.Name];
					if (parentReference != null && (
						(oneNavProp.ToEndMember.RelationshipMultiplicity == RelationshipMultiplicity.One && oneNavProp.ToEndMember.TypeUsage.Facets.Any(f => f.Name == "Nullable" && !(bool)f.Value)) ||
						(oneNavProp.FromEndMember.RelationshipMultiplicity == RelationshipMultiplicity.One && oneNavProp.FromEndMember.TypeUsage.Facets.Any(f => f.Name == "Nullable" && !(bool)f.Value))))
						((EntityModelType) type).OwnerProperties[property] = parentReference as ModelReferenceProperty;
				});
			}

			protected override System.Collections.IList ConvertToList(ModelReferenceProperty property, object list)
			{
				// If the list is managed by Entity Framework, convert to a list with listeners
				if (list is RelatedEnd)
				{
					Type d1 = typeof(CollectionWrapper<>);
					Type constructed = d1.MakeGenericType(((EntityModelType) property.PropertyType).UnderlyingType);

					var constructor = constructed.GetConstructors()[0];
					return (IList) constructor.Invoke(new object[] { list });
				}

				return base.ConvertToList(property, list);
			}

			/// <summary>
			/// Gets or creates the object context for the current scope of work that corresponds to the 
			/// current <see cref="EntityModelType"/>.
			/// </summary>
			/// <returns></returns>
			public IEntityContext GetObjectContext()
			{
				return ((EntityFrameworkModelTypeProvider)Provider).GetObjectContext();
			}

			protected override void SaveInstance(ModelInstance modelInstance)
			{
				try
				{
					GetObjectContext().SaveChanges();
				}
				catch (Exception ex)
				{
					IEnumerable<System.Data.Entity.Validation.DbEntityValidationResult> errors = ((System.Data.Entity.Validation.DbEntityValidationException)ex).EntityValidationErrors;
					throw;
				}
			}

			/// <summary>
			/// Gets the string identifier of the specified instance.
			/// </summary>
			/// <param name="instance"></param>
			/// <returns></returns>
			protected override string GetId(object instance)
			{
				// Get the entity key
				var key = instance is IEntityWithKey ? 
					((IEntityWithKey)instance).EntityKey :
					GetObjectContext().ObjectContext.CreateEntityKey(QualifiedEntitySetName, instance);

				if (key == null || key.IsTemporary)
					return null;
				else if (key.EntityKeyValues.Length > 1)
					return key.EntityKeyValues.Select(v => v.Value.ToString()).Aggregate((v1, v2) => v1 + "," + v2);
				else
					return key.EntityKeyValues[0].Value.ToString();
			}

			/// <summary>
			/// Gets or creates the instance for the specified id.
			/// </summary>
			/// <param name="id">The identifier of an existing instance, or null to create a new instance</param>
			/// <returns>The new or existing instance</returns>
			protected override object GetInstance(string id)
			{
				// Get the current object context
				IEntityContext context = GetObjectContext();

				// Create a new instance if the id is null
				if (id == null)
				{
					// When a new entity is created, it is detached by default.  Attach it to the context so it will be tracked.
					var entity = context.ObjectContext.GetType().GetMethod("CreateObject").MakeGenericMethod(UnderlyingType).Invoke(context.ObjectContext, null);
					context.ObjectContext.AddObject(QualifiedEntitySetName, entity);

					return entity;
				}

				// Otherwise, load the existing instance

				// Split the id string into id tokens
				var tokens = id.Split(',');
				if (tokens.Length != IdProperties.Length)
					throw new ArgumentException("The specified id, '" + id + "', does not have the correct number of key values.");

				// Create an entity key based on the specified id tokens
				var key = new EntityKey(QualifiedEntitySetName,
					IdProperties.Select((property, index) => new EntityKeyMember(property.Name, TypeDescriptor.GetConverter(property.PropertyType).ConvertFromString(tokens[index]))));

				// Attempt to create the entity using the key
				object instance;
				context.ObjectContext.TryGetObjectByKey(key, out instance);
				return instance;
			}

			/// <summary>
			/// Gets the <see cref="EntityState"/> of the specified instance.
			/// </summary>
			/// <param name="instance"></param>
			/// <returns></returns>
			EntityState GetEntityState(object instance)
			{
				if (instance is IModelEntity)
				{
					var changeTracker = ((IModelEntity)instance).ChangeTracker;
					return changeTracker == null ? EntityState.Detached : changeTracker.EntityState;
				}
				else if (instance is ModelEntity)
					return ((ModelEntity)instance).EntityState;
				else
					throw new ArgumentException("The specified entity instance must either be a subclass of ModelEntity or implement IModelEntity.");
			}

			/// <summary>
			/// Indicates whether the specified instance is pending deletion.
			/// </summary>
			/// <param name="instance"></param>
			/// <returns></returns>
			protected override bool GetIsPendingDelete(object instance)
			{
				return GetEntityState(instance).HasFlag(EntityState.Deleted);
			}

			/// <summary>
			/// Sets whether the specified instance is pending deletion.
			/// </summary>
			/// <param name="instance"></param>
			/// <param name="isPendingDelete"></param>
			protected override void SetIsPendingDelete(object instance, bool isPendingDelete)
			{
				// Get the object state from the context
				var state = GetObjectContext().ObjectContext.ObjectStateManager.GetObjectStateEntry(instance);

				// Mark the instance as pending delete
				if (isPendingDelete)
				{
					state.ChangeState(EntityState.Deleted);
					((IModelEntity)instance).IsInitialized = false;
				}

				// Mark the instance as added if the instance is new and is no longer being marked for deletion
				else if (GetId(instance) == null)
					state.ChangeState(EntityState.Added);

				// Otherwise, mark the instance as modified if the instance is existing and is no longer being marked for deletion
				else
					state.ChangeState(EntityState.Modified);
			}

			/// <summary>
			/// Gets the deletion status of the specified instance indicating whether
			/// the instance has been permanently deleted.
			/// </summary>
			/// <param name="instance"></param>
			/// <returns></returns>
			protected override bool GetIsDeleted(object instance)
			{
				return GetEntityState(instance).HasFlag(EntityState.Detached);
			}

			/// <summary>
			/// Gets the underlying modification status of the specified instance,
			/// indicating whether the instance has pending changes that have not been
			/// persisted.
			/// </summary>
			/// <param name="instance"></param>
			/// <returns>True if the instance is new, pending delete, or has unpersisted changes, otherwise false.</returns>
			protected override bool GetIsModified(object instance)
			{
				return !GetEntityState(instance).HasFlag(EntityState.Unchanged);
			}

			internal void RaiseOnSave(ModelInstance instance)
			{
				base.OnSave(instance);
			}
		}
		#endregion

		#region EntityValueProperty

		/// <summary>
		/// Subclass of <see cref="ReflectionReferenceProperty"/> specific to entity models.
		/// </summary>
		/// <remarks>
		/// <see cref="EntityValueProperty"/> supports use of <see cref="DisplayAttribute"/> and <see cref="DisplayFormatAttribute"/>
		/// to provide localized labels and formatting for value properties.
		/// </remarks>
		internal class EntityValueProperty : ReflectionValueProperty
		{
			DisplayAttribute displayAttribute;

			internal EntityValueProperty(ModelType declaringType, PropertyInfo property, string name, string label, string format, bool isStatic, Type propertyType, TypeConverter converter, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
				: base(declaringType, property, name, label, format ?? attributes.OfType<DisplayFormatAttribute>().Select(f => f.DataFormatString).FirstOrDefault(), isStatic, propertyType, converter, isList, isReadOnly, isPersisted, attributes)
			{
				displayAttribute = GetAttributes<DisplayAttribute>().FirstOrDefault();
			}

			/// <summary>
			/// Gets the localized label for properties that have a <see cref="DisplayAttribute"/>.
			/// </summary>
			public override string Label
			{
				get
				{
					return displayAttribute != null ? displayAttribute.GetName() : base.Label;
				}
			}
		}

		#endregion

		#region EntityReferenceProperty

		/// <summary>
		/// Subclass of <see cref="ReflectionReferenceProperty"/> used to track the corresponding relationship and target role name.
		/// </summary>
		internal class EntityReferenceProperty : ReflectionReferenceProperty
		{
			DisplayAttribute displayAttribute;

			internal EntityReferenceProperty(ModelType declaringType, NavigationProperty navProp, PropertyInfo property, string name, string label, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
				: base(declaringType, property, name, label, format, isStatic, propertyType, isList, isReadOnly, isPersisted, attributes)
			{
				RelationshipName = navProp != null ? navProp.RelationshipType.Name : null;
				TargetRoleName = navProp != null ? navProp.ToEndMember.Name : null;
				displayAttribute = GetAttributes<DisplayAttribute>().FirstOrDefault();
			}

			internal string RelationshipName { get; private set; }

			internal string TargetRoleName { get; private set; }

			/// <summary>
			/// Gets the localized label for properties that have a <see cref="DisplayAttribute"/>.
			/// </summary>
			public override string Label
			{
				get
				{
					return displayAttribute != null ? displayAttribute.GetName() : base.Label;
				}
			}
		}

		#endregion
	}
}
