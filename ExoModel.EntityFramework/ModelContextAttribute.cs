﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afterthought;
using System.Data.Objects;
using System.Data.Entity;

namespace ExoModel.EntityFramework
{
	public class ModelContextAttribute : Attribute, IAmendmentAttribute
	{
		IEnumerable<ITypeAmendment> IAmendmentAttribute.GetAmendments(Type target)
		{
			// Ensure the target is a DbContext or ObjectContext subclass
			// Use late-binding approach for DbContext to ensure implementation can still support 4.0 without NuGet EF package
			if ((target.IsSubclassOf(typeof(ObjectContext)) && target != typeof(ModelObjectContext)) || (target.BaseType != null && target.BaseType.FullName == "System.Data.Entity.DbContext"))
			{
				// Determine the set of entity types from the object context target type
				var entityTypes = target.GetProperties()
					.Select(p => p.PropertyType)
					.Where(t => t.IsGenericType && t.GetGenericTypeDefinition().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>)))
					.Select(t => t.GetGenericArguments()[0])
					.ToHashSet();

				// Find all subclasses
				foreach (Type t in target.Assembly.GetTypes())
					EnsureSubTypes(entityTypes, t);

				// Return amendments for each entity type
				foreach (var type in entityTypes)
				{
					var constructor = typeof(EntityAmendment<>).MakeGenericType(type).GetConstructor(new Type[] { typeof(HashSet<Type>) });
					var amendment = constructor.Invoke(new object[] { entityTypes });
					yield return (ITypeAmendment)amendment;
				}

				// Return a context amendment for the DBContext or ObjectContext target
				yield return (ITypeAmendment)typeof(ContextAmendment<>).MakeGenericType(target).GetConstructor(Type.EmptyTypes).Invoke(new object[0]);
			}
		}

		bool EnsureSubTypes(HashSet<Type> types, Type type)
		{
			if (type == null)
				return false;

			if (types.Contains(type))
				return true;

			if (EnsureSubTypes(types, type.BaseType))
			{
				types.Add(type);
				return true;
			}

			return false;
		}
	}
}
