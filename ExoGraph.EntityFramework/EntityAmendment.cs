using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afterthought;
using System.Data.Objects.DataClasses;
using System.Reflection;
using System.Data.Metadata.Edm;
using System.ComponentModel.DataAnnotations;

namespace ExoGraph.EntityFramework
{
	/// <summary>
	/// Amends types after compilation to support Entity Framework and ExoGraph.
	/// </summary>
	/// <typeparam name="TType"></typeparam>
	public class EntityAmendment<TType> : Amendment<TType, IGraphEntity>
	{
		public EntityAmendment(HashSet<Type> entityTypes)
		{
			// IGraphInstance
			Implement<IGraphInstance>(
				Properties.Add<GraphInstance>("Instance", EntityAdapter.InitializeGraphInstance)
			);

			// IGraphEntity
			Implement<IGraphEntity>();

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
					!p.PropertyInfo.GetCustomAttributes(typeof(NotMappedAttribute), true).Any() &&
					// Reference
					entityTypes.Contains(p.Type))
				.Get(EntityAdapter.GetReference)
				.Set(EntityAdapter.SetReference);

			// Unmapped Entity Properties
			Properties
				.Where(p =>
					// Public Read/Write
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic &&
					// Mapped
					p.PropertyInfo.GetCustomAttributes(typeof(NotMappedAttribute), true).Any())
				.BeforeGet(EntityAdapter.BeforeGetReferenceUnmapped)
				.AfterSet(EntityAdapter.AfterSetReferenceUnmapped);


			// List Properties
			Properties
				.Where(p =>
					// Public Read/Write
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic &&
						// List
					(p.Type.IsGenericType && p.Type.GetGenericTypeDefinition() == typeof(ICollection<>) && entityTypes.Contains(p.Type.GetGenericArguments()[0])))
				.Get(EntityAdapter.GetList);

			// Mapped Value Properties
			Properties
				.Where(p =>
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic && !p.PropertyInfo.GetCustomAttributes(typeof(NotMappedAttribute), true).Any() &&
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
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic && p.PropertyInfo.GetCustomAttributes(typeof(NotMappedAttribute), true).Any() &&
						// Not Reference
					!entityTypes.Contains(p.Type) &&
						// Not List
					!(p.Type.IsGenericType && p.Type.GetGenericTypeDefinition() == typeof(ICollection<>) && entityTypes.Contains(p.Type.GetGenericArguments()[0])))
				.BeforeGet(EntityAdapter.BeforeGetValue)
				.AfterSet(EntityAdapter.AfterSetValueUnmapped);
		}
	}
}

