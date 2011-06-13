using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afterthought;
using System.Data.Objects.DataClasses;
using System.Reflection;
using System.Data.Metadata.Edm;

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

			// Entity Properties
			Properties
				.Where(p =>
					// Public Read/Write
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic &&
						// Reference
					entityTypes.Contains(p.Type))
				.Get(EntityAdapter.GetReference)
				.Set(EntityAdapter.SetReference);


			// List Properties
			Properties
				.Where(p =>
					// Public Read/Write
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic &&
						// List
					(p.Type.IsGenericType && p.Type.GetGenericTypeDefinition() == typeof(ICollection<>) && entityTypes.Contains(p.Type.GetGenericArguments()[0])))
				.Get(EntityAdapter.GetList);

			// Value Properties
			Properties
				.Where(p =>
					// Public Read/Write
					p.PropertyInfo != null && p.PropertyInfo.CanRead && p.PropertyInfo.CanWrite && p.PropertyInfo.GetGetMethod().IsPublic &&
						// Not Reference
					!entityTypes.Contains(p.Type) &&
						// Not List
					!(p.Type.IsGenericType && p.Type.GetGenericTypeDefinition() == typeof(ICollection<>) && entityTypes.Contains(p.Type.GetGenericArguments()[0])))
				.BeforeGet(EntityAdapter.BeforeGetValue)
				.BeforeSet(EntityAdapter.BeforeSetValue)
				.AfterSet(EntityAdapter.AfterSetValue);
		}
	}
}

