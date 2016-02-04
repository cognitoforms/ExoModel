using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ExoModel.Json
{
	public static class JsonEntityAdapter<TEntity>
		where TEntity : IJsonEntity
	{
		/// <summary>
		/// Creates a new <see cref="ModelInstance"/> for the specified entity.
		/// </summary>
		public static ModelInstance InitializeModelInstance(TEntity entity, string property)
		{
			return new ModelInstance(entity);
		}

		internal static object CreateList(Type itemType)
		{
			var listType = typeof(ObservableCollection<>).MakeGenericType(itemType);

			var listCtor = listType.GetConstructor(Type.EmptyTypes);
			if (listCtor == null)
				throw new Exception("List type '" + listType.Name + "' must have a default constructor.");

			return listCtor.Invoke(new object[0]);
		}

		public static void BeforeGet(TEntity entity, string property)
		{
			if (!entity.IsInitialized.HasValue)
			{
				entity.IsInitialized = false;

				if (entity.Id.HasValue)
					JsonEntity.OnInitExisting(entity);
				else
					JsonEntity.OnInitNew(entity);

				// Initialize reference list properties.
				foreach (var p in entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
				{
					Type listItemType;
					if (JsonEntity.TryGetListItemType(p.PropertyType, out listItemType))
					{
						var list = p.GetValue(entity, null);
						if (list == null)
							p.SetValue(entity, CreateList(listItemType), null);
					}
				}

				entity.IsInitialized = true;
			}

			if (entity.IsInitialized.Value)
			{
				// Raise property get notifications
				((IModelInstance) entity).Instance.OnPropertyGet(property);
			}
		}

		/// <summary>
		/// Raises property set when a property setter is called.
		/// </summary>
		public static void AfterSet<TProperty>(TEntity entity, string property, TProperty oldValue, TProperty value, TProperty newValue)
		{
			if (entity.IsInitialized.HasValue && entity.IsInitialized.Value)
			{
				// Raise property change notifications
				((IModelInstance) entity).Instance.OnPropertySet(property, oldValue, newValue);
			}
		}

		public static object BeforeSetId(TEntity entity, string property, object oldValue, object newValue)
		{
			if (property != "Id")
				throw new InvalidOperationException("Amendment 'AfterSetId' called for property '" + property + "'.");

			var oldId = (int?)oldValue;
			var newId = (int?)newValue;

			if (entity.IsInitialized.HasValue && entity.IsInitialized.Value)
				throw new Exception("Cannot set id of initialized entity '" + entity.GetType().Name + "|" + (entity.Id == null ? "<null>" : entity.Id.ToString()) + "'.");

			if (oldId.HasValue || !newId.HasValue)
				throw new Exception("Cannot change id from " + (oldValue == null ? "<null>" : oldValue.ToString()) + " to " + (newValue == null ? "<null>" : newValue.ToString()) + ".");

			return newValue;
		}
	}
}
