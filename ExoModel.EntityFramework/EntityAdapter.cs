using System.Data.Objects.DataClasses;
using System.Reflection;
using System.Data.Objects;
using System;

namespace ExoModel.EntityFramework
{
	public static class EntityAdapter
	{
		static MethodInfo getRelatedEnd = 
			typeof(RelationshipManager).GetMethod("GetRelatedEnd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new System.Type[] { typeof(string) }, null) ??
			typeof(RelationshipManager).GetMethod("GetRelatedEnd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new System.Type[] { typeof(string), typeof(bool) }, null);

		static bool oneParamRelatedEnd = getRelatedEnd.GetParameters().Length == 1;

		/// <summary>
		/// Creates a new <see cref="ModelInstance"/> for the specified instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		public static ModelInstance InitializeModelInstance(IModelEntity instance, string property)
		{
			return new ModelInstance(instance);
		}

		public static ObjectContext GetObjectContext(IEntityContext context, string property)
		{
			// Lazy-load EF 4.1 or 4.2
			var contextAdapter = Type.GetType("System.Data.Entity.Infrastructure.IObjectContextAdapter, EntityFramework, Version=4.3.1.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", false);
			if (contextAdapter != null && contextAdapter.IsAssignableFrom(context.GetType()))
				return (ObjectContext)contextAdapter.GetProperty("ObjectContext").GetValue(context, null);
                
			contextAdapter = Type.GetType("System.Data.Entity.Infrastructure.IObjectContextAdapter, EntityFramework, Version=4.2.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", false);
			if (contextAdapter != null && contextAdapter.IsAssignableFrom(context.GetType()))
				return (ObjectContext)contextAdapter.GetProperty("ObjectContext").GetValue(context, null);

			contextAdapter = Type.GetType("System.Data.Entity.Infrastructure.IObjectContextAdapter, EntityFramework, Version=4.1.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", false);
			if (contextAdapter != null && contextAdapter.IsAssignableFrom(context.GetType()))
				return (ObjectContext)contextAdapter.GetProperty("ObjectContext").GetValue(context, null);

			if (context is ObjectContext)
				return context as ObjectContext;

			throw new InvalidOperationException("An object context could not be obtained from the specified entity context.");
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
		public static RelationshipManager InitializeRelationshipManager(IModelEntity instance, string property)
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
		public static void SetChangeTracker(IModelEntity instance, IEntityChangeTracker changeTracker)
		{
			instance.ChangeTracker = changeTracker;
			instance.IsInitialized = true;
		}

		static IRelatedEnd GetRelatedEnd(IModelEntity instance, string property)
		{
			return oneParamRelatedEnd ?
				(IRelatedEnd)getRelatedEnd.Invoke(instance.RelationshipManager, new object[] { property }) :
				(IRelatedEnd)getRelatedEnd.Invoke(instance.RelationshipManager, new object[] { property, false });
		}

		/// <summary>
		/// Gets a navigation property reference for the specified property.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		public static object GetReference(IModelEntity instance, string property)
		{
			// Raise property get notifications
			instance.Instance.OnPropertyGet(property);

			// Return the property reference
			var reference = GetRelatedEnd(instance, property).GetEnumerator();
			reference.MoveNext();
			return reference.Current;
		}

		/// <summary>
		/// Gets a navigation property reference for the specified property.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		public static void BeforeGetReferenceUnmapped(IModelEntity instance, string property)
		{
			// Raise property get notifications
			instance.Instance.OnPropertyGet(property);
		}

		/// <summary>
		/// Gets a navigation property reference for the specified property.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		public static object GetList(IModelEntity instance, string property)
		{
			// Raise property get notifications
			instance.Instance.OnPropertyGet(property);

			// Get the property reference
			var reference = GetRelatedEnd(instance, property);

			// Load the reference if necessary
			if (!reference.IsLoaded && !(instance.ChangeTracker.EntityState == System.Data.EntityState.Added 
										|| instance.ChangeTracker.EntityState == System.Data.EntityState.Detached))
				reference.Load();

			// Return the reference
			return reference;
		}

		/// <summary>
		/// Sets a navigation property reference for the specified property.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <param name="value"></param>
		public static void SetReference(IModelEntity instance, string property, object value)
		{
			// Ignore reference setting before the instance is initialized
			if (!instance.IsInitialized)
				return;

			// Touch the Id to ensure that the value is initialized
			if (value != null && !((IModelEntity) value).IsInitialized)
			{
				var id = ((IModelEntity) value).Instance.Id;
			}

			// Get the entity reference
			var reference = GetRelatedEnd(instance, property);

			// Track the current value
			var set = reference.GetEnumerator();
			set.MoveNext();
			var oldValue = set.Current;

			// Update the reference if it is being assigned a different value
			if ((oldValue == null ^ value == null) || (oldValue != null && !oldValue.Equals(value)))
			{
				if (oldValue != null)
					reference.Remove((IModelEntity)oldValue);
				if (value != null)
					reference.Add((IModelEntity)value);

				// Raise property change notifications
				instance.Instance.OnPropertyChanged(property, oldValue, value);
			}
		}

		/// <summary>
		/// Raise property set notification when an unmapped reference changes
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <param name="oldValue"></param>
		/// <param name="value"></param>
		/// <param name="newValue"></param>
		public static void AfterSetReferenceUnmapped(IModelEntity instance, string property, object oldValue, object value, object newValue)
		{
			// Ignore changes to uninitialized instances
			if (instance.IsInitialized)
			{
				// Raise property change notifications
				instance.Instance.OnPropertyChanged(property, oldValue, value);
			}
		}


		/// <summary>
		/// Notifies ExoModel that a value property is being accessed.
		/// </summary>
		/// <typeparam name="TProperty"></typeparam>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		public static void BeforeGetValue(IModelEntity instance, string property)
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
		public static TProperty BeforeSetValueMapped<TProperty>(IModelEntity instance, string property, TProperty oldValue, TProperty value)
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
		public static void AfterSetValueMapped<TProperty>(IModelEntity instance, string property, TProperty oldValue, TProperty value, TProperty newValue)
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

		/// <summary>
		/// Raise member changed events and property change notifications after a property is set.
		/// </summary>
		/// <typeparam name="TProperty"></typeparam>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <param name="oldValue"></param>
		/// <param name="value"></param>
		/// <param name="newValue"></param>
		public static void AfterSetValueUnmapped<TProperty>(IModelEntity instance, string property, TProperty oldValue, TProperty value, TProperty newValue)
		{
			// Ignore changes to uninitialized instances
			if (instance.IsInitialized)
			{
				// Raise property change notifications
				instance.Instance.OnPropertySet(property, oldValue, newValue);
			}
		}
	}
}
