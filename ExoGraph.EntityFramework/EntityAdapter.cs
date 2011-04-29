using System.Data.Objects.DataClasses;
using System.Reflection;
using System.Data.Objects;
using System;

namespace ExoGraph.EntityFramework
{
	public static class EntityAdapter
	{
		static MethodInfo getRelatedEnd = typeof(RelationshipManager).GetMethod("GetRelatedEnd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new System.Type[] { typeof(string) }, null);

		/// <summary>
		/// Creates a new <see cref="GraphInstance"/> for the specified instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		public static GraphInstance InitializeGraphInstance(IGraphEntity instance, string property)
		{
			return new GraphInstance(instance);
		}

		public static ObjectContext GetObjectContext(IEntityContext context, string property)
		{
			// Lazy-load EF 4.1
			var contextAdapter = Type.GetType("System.Data.Entity.Infrastructure.IObjectContextAdapter, EntityFramework, Version=4.1.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
			if (contextAdapter.IsAssignableFrom(context.GetType()))
				return (ObjectContext)contextAdapter.GetProperty("ObjectContext").GetValue(context, null);
			return context as ObjectContext;
		}

		public static int AfterSaveChanges(IEntityContext context, int result)
		{
			context.OnSavedChanges();
			return result;
		}

		/// <summary>
		/// Creates a new <see cref="RelationshipManager"/> for the specified instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		public static RelationshipManager InitializeRelationshipManager(IGraphEntity instance, string property)
		{
			return RelationshipManager.Create(instance);
		}

		/// <summary>
		/// Allows a <see cref="IEntityChangeTracker"/> to be assigned to the specified instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		public static void SetChangeTracker(IGraphEntity instance, IEntityChangeTracker changeTracker)
		{
			instance.ChangeTracker = changeTracker;
			instance.IsInitialized = true;
		}

		/// <summary>
		/// Gets a navigation property reference for the specified property.
		/// </summary>
		/// <typeparam name="TRef"></typeparam>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		public static TRef GetReference<TRef>(IGraphEntity instance, string property)
			where TRef : class
		{
			// Raise property get notifications
			instance.Instance.OnPropertyGet(property);

			// Return the property reference
			var graphProperty = (EntityFrameworkGraphTypeProvider.EntityReferenceProperty)instance.Instance.Type.Properties[property];
			return instance.RelationshipManager.GetRelatedReference<TRef>(graphProperty.RelationshipName, graphProperty.TargetRoleName).Value;
		}

		/// <summary>
		/// Gets a navigation property reference for the specified property.
		/// </summary>
		/// <typeparam name="TList"></typeparam>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		public static TList GetList<TList>(IGraphEntity instance, string property)
		{
			// Raise property get notifications
			instance.Instance.OnPropertyGet(property);

			// Get the property reference
			var reference = (IRelatedEnd)getRelatedEnd.Invoke(instance.RelationshipManager, new object[] { property });

			// Load the reference if necessary
			if (!reference.IsLoaded)
				reference.Load();

			// Return the reference
			return (TList)reference;
		}

		/// <summary>
		/// Sets a navigation property reference for the specified property.
		/// </summary>
		/// <typeparam name="TRef"></typeparam>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <param name="value"></param>
		public static void SetReference<TRef>(IGraphEntity instance, string property, TRef value)
			where TRef : class
		{
			// Ignore reference setting before the instance is initialized
			if (!instance.IsInitialized)
				return;

			// Get the entity reference
			var reference = (EntityReference<TRef>)getRelatedEnd.Invoke(instance.RelationshipManager, new object[] { property });

			// Track the current value
			var oldValue = reference.Value;

			// Update the reference if it is being assigned a different value
			if ((oldValue == null ^ value == null) || (oldValue != null && !oldValue.Equals(value)))
			{
				// Assign the new value to the reference
				reference.Value = value;

				// Raise property change notifications
				instance.Instance.OnPropertyChanged(property, oldValue, value);
			}
		}

		/// <summary>
		/// Notifies ExoGraph that a value property is being accessed.
		/// </summary>
		/// <typeparam name="TProperty"></typeparam>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		public static void BeforeGetValue<TProperty>(IGraphEntity instance, string property)
		{
			// Raise property get notifications for initialized instances
			if (instance.IsInitialized)
				instance.Instance.OnPropertyGet(property);
		}

		/// <summary>
		/// Raises member changing events before a value property is set.
		/// </summary>
		/// <typeparam name="TProperty"></typeparam>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <param name="oldValue"></param>
		/// <param name="value"></param>
		public static TProperty BeforeSetValue<TProperty>(IGraphEntity instance, string property, TProperty oldValue, TProperty value)
		{
			// Notify the change tracker that the property is changing
			if (instance.IsInitialized)
				instance.ChangeTracker.EntityMemberChanging(property);

			// Return the unmodified value
			return value;
		}

		/// <summary>
		/// Raise member changed events and property change notifications after a property is set.
		/// </summary>
		/// <typeparam name="TProperty"></typeparam>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <param name="oldValue"></param>
		/// <param name="value"></param>
		/// <param name="newValue"></param>
		public static void AfterSetValue<TProperty>(IGraphEntity instance, string property, TProperty oldValue, TProperty value, TProperty newValue)
		{
			// Ignore changes to uninitialized instances
			if (instance.IsInitialized)
			{
				// Notify the change tracker that the property has changed
				instance.ChangeTracker.EntityMemberChanged(property);

				// Raise property change notifications
				instance.Instance.OnPropertySet(property, oldValue, newValue);
			}
		}
	}
}
