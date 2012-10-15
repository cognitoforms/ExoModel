using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afterthought;
using System.Data.Objects.DataClasses;
using System.Reflection;
using System.Data.Metadata.Edm;
using System.ComponentModel.DataAnnotations;

namespace ExoModel.EntityFramework
{
	/// <summary>
	/// Amends types after compilation to support Entity Framework and ExoModel.
	/// </summary>
	/// <typeparam name="TType"></typeparam>
	public class EntityAmendment<TType> : Amendment<TType, IModelEntity>
	{
		public EntityAmendment(HashSet<Type> entityTypes)
		{
			// IModelInstance
			Implement<IModelInstance>(
				Properties.Add<ModelInstance>("Instance", EntityAdapter.InitializeModelInstance)
			);

			// IModelEntity
			Implement<IModelEntity>();

			// IEntityWithKey
			Implement<IEntityWithKey>();

			// IEntityWithRelationships
			Implement<IEntityWithRelationships>(
				Properties.Add<RelationshipManager>("RelationshipManager", EntityAdapter.InitializeRelationshipManager)
			);

			// IEntityChangeTracker
			Implement<IEntityWithChangeTracker>(
				Methods.Add<IEntityChangeTracker>("SetChangeTracker", EntityAdapter.SetChangeTracker)
			);

			// Mapped Entity Properties
			Properties
				.Where(p =>
					// Public Read/Write
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic &&
					// Mapped
					IsMapped(p.PropertyInfo) &&
					// Reference
					entityTypes.Contains(p.Type))
				.Get(EntityAdapter.GetReference)
				.Set(EntityAdapter.SetReference);

			// Unmapped Entity Properties
			Properties
				.Where(p =>
					// Public Read/Write
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic &&
					// Not Mapped
					!IsMapped(p.PropertyInfo))
				.BeforeGet(EntityAdapter.BeforeGetReferenceUnmapped)
				.AfterSet(EntityAdapter.AfterSetReferenceUnmapped);

			// Mapped List Properties
			Properties
				.Where(p =>
					// Public Read/Write
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic && IsMapped(p.PropertyInfo) &&
						// List
					(p.Type.IsGenericType && p.Type.GetGenericTypeDefinition() == typeof(ICollection<>) && entityTypes.Contains(p.Type.GetGenericArguments()[0])))
				.Get(EntityAdapter.GetList);

			// Mapped Value Properties
			Properties
				.Where(p =>
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic && IsMapped(p.PropertyInfo) &&
					!entityTypes.Contains(p.Type) &&
						// Not List
					!(p.Type.IsGenericType && p.Type.GetGenericTypeDefinition() == typeof(ICollection<>) && entityTypes.Contains(p.Type.GetGenericArguments()[0])))
				.BeforeGet(EntityAdapter.BeforeGetValue)
				.BeforeSet(EntityAdapter.BeforeSetValueMapped)
				.AfterSet(EntityAdapter.AfterSetValueMapped);

			// Unmapped Value Properties
			Properties
				.Where(p =>
					// Public Read/Write
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic && !IsMapped(p.PropertyInfo) &&
						// Not Reference
					!entityTypes.Contains(p.Type) &&
						// Not List
					!(p.Type.IsGenericType && p.Type.GetGenericTypeDefinition() == typeof(ICollection<>) && entityTypes.Contains(p.Type.GetGenericArguments()[0])))
				.BeforeGet(EntityAdapter.BeforeGetValue)
				.AfterSet(EntityAdapter.AfterSetValueUnmapped);
		}

		/// <summary>
		/// Determines whether the specified property is considered mapped by EF.
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		bool IsMapped(PropertyInfo property)
		{
			return !property.GetCustomAttributes(true).Any(a => a.GetType().FullName == "System.ComponentModel.DataAnnotations.NotMappedAttribute");
		}
	}
}

