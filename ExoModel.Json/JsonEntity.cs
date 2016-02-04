using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ExoModel.Json
{
	public abstract class JsonEntity : IJsonEntity
	{
		public int? Id { get; private set; }

		public bool? IsInitialized { get; private set; }

		~JsonEntity()
		{
			if (Finalized != null)
				Finalized(this, EventArgs.Empty);
		}

		public event EventHandler Finalized;

		int? IJsonEntity.Id
		{
			get { return Id; }
			set { Id = value; }
		}

		bool? IJsonEntity.IsInitialized
		{
			get { return IsInitialized; }
			set { IsInitialized = value; }
		}

		internal static bool TryGetReferenceType(Type type, ICollection<Type> referenceTypes, out Type referenceType, out bool isList)
		{
			Type innerType;
			if (type == typeof(string) || !(typeof(IEnumerable).IsAssignableFrom(type)) || !TryGetListItemType(type, out innerType))
			{
				isList = false;
				innerType = type;
			}
			else
			{
				isList = true;
			}

			if (referenceTypes.Contains(innerType))
			{
				referenceType = innerType;
				return true;
			}

			referenceType = null;
			return false;
		}

		internal static bool TryGetListItemType(Type listType, out Type itemType)
		{
			// First see if the type is ICollection<T>
			if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(ICollection<>))
			{
				itemType = listType.GetGenericArguments()[0];
				return true;
			}

			// Then see if the type implements ICollection<T>
			foreach (var interfaceType in listType.GetInterfaces())
			{
				if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(ICollection<>))
				{
					itemType = interfaceType.GetGenericArguments()[0];
					return true;
				}
			}

			// Then see if the type implements IList and has a strongly-typed Item property indexed by an integer value
			if (typeof(IList).IsAssignableFrom(listType))
			{
				var itemProperty = listType.GetProperty("Item", new[] { typeof(int) });
				if (itemProperty != null)
				{
					itemType = itemProperty.PropertyType;
					return true;
				}
			}

			// Return false to indicate that the specified type is not a supported list type
			itemType = null;
			return false;
		}

		internal static void OnInitNew(IJsonEntity entity)
		{
			JsonEntityContext.GetContextForEntity(entity).Add(entity);
		}

		private static ConstructorInfo GetDefaultConstructor(Type type, IDictionary<Type, ConstructorInfo> defaultConstructorCache = null)
		{
			ConstructorInfo ctor;
			if (defaultConstructorCache == null || !defaultConstructorCache.TryGetValue(type, out ctor))
			{
				ctor = type.GetConstructor(Type.EmptyTypes);
				if (ctor == null)
					throw new Exception("Type '" + type.Name + "' must have a public parameterless constructor.");

				if (defaultConstructorCache != null)
					defaultConstructorCache[type] = ctor;
			}

			return ctor;
		}

		internal static IJsonEntity CreateNew(Type type, IDictionary<Type, ConstructorInfo> defaultConstructorCache = null)
		{
			var ctor = GetDefaultConstructor(type, defaultConstructorCache);
			return (IJsonEntity)ctor.Invoke(null);
		}

		internal static void OnInitExisting(IJsonEntity entity)
		{
			JsonEntityContext.GetContextForEntity(entity).Initialize(entity);
		}

		internal static IJsonEntity CreateExisting(Type type, int id, IDictionary<Type, ConstructorInfo> defaultConstructorCache = null)
		{
			var ctor = GetDefaultConstructor(type, defaultConstructorCache);
			var entity = (IJsonEntity) ctor.Invoke(null);
			entity.Id = id;
			return entity;
		}
	}
}
