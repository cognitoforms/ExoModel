using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace ExoModel.Json
{
	public class JsonEntityContext
	{
		[ThreadStatic]
		private static Storage _globalStorage;

		private readonly string storagePath;

		private readonly bool autoInitializeEntities;

		private readonly Type[] supportedTypes;

		private readonly Dictionary<Type, ConstructorInfo> _defaultConstructors; 

		private readonly List<IJsonEntity> _newEntities;

		private readonly List<IJsonEntity> _pendingDeleteEntities;

		private readonly List<IJsonEntity> _deletedEntities;

		private readonly Dictionary<Type, Dictionary<int, IJsonEntity>> _existingEntities;

		private readonly Dictionary<Type, Dictionary<string, Dictionary<string, object>>> _instanceData;

		internal JsonEntityContext(string storagePath, bool autoInitializeEntities, params Type[] supportedTypes)
		{
			this.storagePath = storagePath;
			this.autoInitializeEntities = autoInitializeEntities;
			this.supportedTypes = supportedTypes;

			_defaultConstructors = new Dictionary<Type, ConstructorInfo>();
			_existingEntities = new Dictionary<Type, Dictionary<int, IJsonEntity>>();
			_instanceData = new Dictionary<Type, Dictionary<string, Dictionary<string, object>>>();
			_newEntities = new List<IJsonEntity>();
			_pendingDeleteEntities = new List<IJsonEntity>();
			_deletedEntities = new List<IJsonEntity>();

			GetStorage().Register(this, supportedTypes);
		}

		private class Storage
		{
			internal readonly IDictionary<Type, JsonEntityContext> TypeContexts = new Dictionary<Type, JsonEntityContext>();

			internal void Register(JsonEntityContext context, IEnumerable<Type> supportedTypes)
			{
				foreach (var type in supportedTypes)
					TypeContexts[type] = context;
			}
		}

		private static Storage GetStorage()
		{
			if (_globalStorage == null)
				_globalStorage = new Storage();

			return _globalStorage;
		}

		internal static JsonEntityContext GetContextForEntity(object entity)
		{
			var storage = GetStorage();

			var entityType = entity.GetType();

			JsonEntityContext context;
			if (!storage.TypeContexts.TryGetValue(entityType, out context))
				throw new Exception("Cannot get entity context for type '" + entityType.Name + "'.");

			return context;
		}

		private Dictionary<string, Dictionary<string, object>> GetTypeData(Type type, bool createFile = false)
		{
			Dictionary<string, Dictionary<string, object>> typeData;
			if (!_instanceData.TryGetValue(type, out typeData))
			{
				string json;

				var jsonFilePath = Path.Combine(storagePath, type.Name + ".json");

				if (File.Exists(jsonFilePath))
					json = File.ReadAllText(jsonFilePath);
				else
				{
					if (createFile)
					{
						json = "{\r\n}\r\n";
						File.WriteAllText(jsonFilePath, json);
					}
					else
						return null;
				}

				typeData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(json);

				_instanceData[type] = typeData;

				if (typeData == null)
					return null;
			}

			return typeData;
		}

		public T Fetch<T>(int id)
		{
			return (T)Fetch(typeof(T), id);
		}

		public object Fetch(Type type, int id)
		{
			if (!supportedTypes.Contains(type))
				throw new NotSupportedException("Type '" + type.Name + "' is not supported.");

			IJsonEntity entity;

			Dictionary<int, IJsonEntity> typeEntities;
			if (!_existingEntities.TryGetValue(type, out typeEntities))
				_existingEntities[type] = typeEntities = new Dictionary<int, IJsonEntity>();
			else if (typeEntities.TryGetValue(id, out entity))
				return entity;

			Dictionary<string, object> entityData;

			var typeData = GetTypeData(type);
			if (!typeData.TryGetValue(id.ToString(CultureInfo.InvariantCulture), out entityData))
				return null;

			entity = JsonEntity.CreateExisting(type, id, _defaultConstructors);

			typeEntities[id] = entity;

			if (autoInitializeEntities)
				InitializeInstance(type, entity, entityData);

			return entity;
		}

		public IEnumerable<T> FetchAll<T>()
		{
			return FetchAll(typeof (T)).Cast<T>();
		}

		public IEnumerable FetchAll(Type type)
		{
			if (!supportedTypes.Contains(type))
				throw new NotSupportedException("Type '" + type.Name + "' is not supported.");

			Dictionary<int, IJsonEntity> typeEntities;
			if (!_existingEntities.TryGetValue(type, out typeEntities))
			{
				typeEntities = new Dictionary<int, IJsonEntity>();
				_existingEntities[type] = typeEntities;
			}

			var typeData = GetTypeData(type);

			lock (typeData)
			{
				foreach (var key in typeData.Keys)
				{
					var id = int.Parse(key);

					var entity = JsonEntity.CreateExisting(type, id, _defaultConstructors);

					typeEntities[id] = entity;

					if (autoInitializeEntities)
						InitializeInstance(type, entity, typeData[key]);
				}
			}

			return typeEntities.Values;
		}

		internal void Initialize(IJsonEntity entity)
		{
			var type = entity.GetType();

			var id = entity.Id;
			if (!id.HasValue)
				throw new InvalidOperationException("Cannot initialize entity of type '" + type.Name + "' with no id.");

			Dictionary<string, object> instanceData;

			var typeData = GetTypeData(type);
			if (!typeData.TryGetValue(id.Value.ToString(CultureInfo.InvariantCulture), out instanceData))
				throw new InvalidOperationException("Cannot initialize entity: " + type.Name + "|" + id + ".");

			InitializeInstance(type, entity, instanceData);
		}

		private void InitializeInstance(IReflect type, IJsonEntity entity, Dictionary<string, object> data)
		{
			var jsonProperties = data.Keys.ToArray();

			foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				var propertyType = property.PropertyType;

				Type referenceType;
				bool isReferenceList;

				object propData;
				if (jsonProperties.Contains(property.Name))
					propData = data[property.Name];
				else
				{
					propData = null;

					if (JsonEntity.TryGetReferenceType(propertyType, supportedTypes, out referenceType, out isReferenceList))
					{
						if (isReferenceList)
						{
							var list = JsonEntityAdapter<JsonEntity>.CreateList(referenceType);
							property.SetValue(entity, list, null);
						}
					}
				}

				if (propData == null)
					continue;

				object value;

				if (JsonEntity.TryGetReferenceType(propertyType, supportedTypes, out referenceType, out isReferenceList))
				{
					if (isReferenceList)
					{
						var list = JsonEntityAdapter<JsonEntity>.CreateList(referenceType);

						var addMethod = list.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public, null, new[] {referenceType}, null);

						foreach (var referenceId in (IEnumerable)propData)
						{
							var reference = Fetch(referenceType, (int)Convert.ChangeType(referenceId, typeof(int)));
							if (reference == null)
								throw new InvalidOperationException();

							addMethod.Invoke(list, new[] { reference });
						}

						value = list;
					}
					else
					{
						var referenceId = (int)Convert.ChangeType(propData, typeof(int));

						value = Fetch(referenceType, referenceId);
					}
				}
				else
				{
					Type targetType;
					if (!TryGetNullableType(propertyType, out targetType))
						targetType = propertyType;

					if (targetType == typeof(DateTime) && propData is string)
						value = DateTime.Parse((string)propData);
					else if (propData.GetType() != targetType)
						value = Convert.ChangeType(propData, targetType);
					else
						value = propData;
				}

				property.SetValue(entity, value, null);
			}
		}

		private static bool TryGetNullableType(Type type, out Type nullableType)
		{
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				nullableType = type.GetGenericArguments()[0];
				return true;
			}

			nullableType = null;
			return false;
		}

		internal void Add<T>(T entity)
			where T : IJsonEntity
		{
			Add(typeof(T), entity);
		}

		internal void Add(Type type, IJsonEntity entity)
		{
			if (!_newEntities.Contains(entity))
				_newEntities.Add(entity);
		}

		internal bool IsPendingDelete(IJsonEntity entity)
		{
			return _pendingDeleteEntities.Contains(entity);
		}

		internal void SetPendingDelete(IJsonEntity entity, bool value)
		{
			if (value && !_pendingDeleteEntities.Contains(entity))
				_pendingDeleteEntities.Add(entity);
			else if (!value && _pendingDeleteEntities.Contains(entity))
				_pendingDeleteEntities.Remove(entity);
		}

		internal bool IsDeleted(IJsonEntity entity)
		{
			return _deletedEntities.Contains(entity);
		}

		internal bool Save(IJsonEntity entity)
		{
			Type[] affectedTypes;
			object[] addedEntities;
			object[] removedEntities;

			var type = entity.GetType();

			UpdateData(type, entity, true, out affectedTypes, out addedEntities, out removedEntities);

			var saved = false;

			foreach (var updatedType in affectedTypes)
			{
				var typeData = GetTypeData(updatedType);

				var json = new StringBuilder();

				json.AppendLine("{");

				var keys = typeData.Keys.Select(int.Parse).OrderBy(k => k).ToArray();

				for (var i = 0; i < keys.Length; i++)
				{
					var id = keys[i].ToString(CultureInfo.InvariantCulture);

					var rawJson = JsonConvert.SerializeObject(typeData[id], new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });

					var formattedJson = Regex.Replace(Regex.Replace(Regex.Replace(rawJson, "^{(.*)}$", "{ $1 }"), "\":", "\": "), ",\"", ", \"");

					json.AppendFormat("    \"{0}\": {1}", id, formattedJson);

					if (i == keys.Length - 1)
						json.AppendLine();
					else
						json.AppendLine(",");
				}

				json.AppendLine("}");

				if (!Directory.Exists(storagePath))
					Directory.CreateDirectory(storagePath);

				var jsonFilePath = Path.Combine(storagePath, type.Name + ".json");

				File.WriteAllText(jsonFilePath, json.ToString(), Encoding.UTF8);

				saved = true;
			}

			return saved;
		}

		private static bool IsReferenceModified(int? referenceId, object propertyData)
		{
			return referenceId != (int?) propertyData;
		}

		private static bool IsReferenceListModified(ICollection<int> referenceIds, object propertyData)
		{
			if (propertyData == null)
				return true;

			var persistedReferences = ((IEnumerable) propertyData).Cast<object>().Select(i => (int)Convert.ChangeType(i, typeof (int))).ToArray();

			return string.Join(",", referenceIds) != string.Join(",", persistedReferences);
		}

		private static bool IsValueModified(object value, object propertyData)
		{
			return value != propertyData;
		}

		private void UpdateData(Type type, IJsonEntity entity, bool allowDelete, out Type[] affectedTypes, out object[] addedEntities, out object[] removedEntities)
		{
			var affected = new List<Type>();
			var added = new List<object>();
			var removed = new List<object>();

			var typeData = GetTypeData(type, true);

			if (_deletedEntities.Contains(entity))
			{
				var id = entity.Id;

				if (!id.HasValue)
					throw new Exception("Found deleted entity with no id: " + type.Name + "|.");

				throw new Exception("Found deleted entity: " + type.Name + "|" + id + ".");
			}

			if (_pendingDeleteEntities.Contains(entity))
			{
				var id = entity.Id;

				if (!allowDelete)
					throw new Exception("Cannot delete entity: " + type.Name + "|" + id + ".");

				// Only need to delete Entities that were actually saved previously.
				if (id.HasValue)
				{
					if (typeData.ContainsKey(id.Value.ToString(CultureInfo.InvariantCulture)))
						typeData.Remove(id.Value.ToString(CultureInfo.InvariantCulture));

					removed.Add(entity);
					affected.Add(type);

					if (!_deletedEntities.Contains(entity))
						_deletedEntities.Add(entity);
				}

				_pendingDeleteEntities.Remove(entity);
			}
			else
			{
				var updated = false;

				Dictionary<string, object> instanceData;

				if (_newEntities.Contains(entity))
				{
					int newId;

					if (typeData.Count == 0)
						newId = 1;
					else
					{
						var largestId = typeData.Keys.Max(k => int.Parse(k));
						newId = largestId + 1;
					}

					entity.Id = newId;

					typeData[newId.ToString(CultureInfo.InvariantCulture)] = instanceData = new Dictionary<string, object>();

					_newEntities.Remove(entity);

					added.Add(entity);

					Dictionary<int, IJsonEntity> instanceCache;
					if (!_existingEntities.TryGetValue(type, out instanceCache))
						_existingEntities[type] = instanceCache = new Dictionary<int, IJsonEntity>();

					instanceCache[newId] = entity;

					updated = true;
				}
				else
				{
					var id = entity.Id;

					if (!id.HasValue)
						throw new Exception("Found existing entity with no id: " + type.Name + "|.");

					if (!typeData.TryGetValue(id.Value.ToString(CultureInfo.InvariantCulture), out instanceData))
						throw new Exception("Couldn't find data for existing entity: " + type.Name + "|.");
				}

				foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
				{
					if (property.Name == "Id")
						continue;

					var value = property.GetValue(entity, null);

					object serializedValue;

					var propertyType = property.PropertyType;

					object propertyData;
					var hasPropertyData = instanceData.TryGetValue(property.Name, out propertyData);

					Type referenceType;
					bool isReferenceList;
					if (JsonEntity.TryGetReferenceType(propertyType, supportedTypes, out referenceType, out isReferenceList))
					{
						if (isReferenceList)
						{
							var references = ((IEnumerable)value).Cast<IJsonEntity>().ToArray();
							var referenceIds = new List<int>();

							foreach (var reference in references)
							{
								Type[] refAffectedTypes;
								object[] refAdded;
								object[] refRemoved;

								UpdateData(referenceType, reference, false, out refAffectedTypes, out refAdded, out refRemoved);

								affected.AddRange(refAffectedTypes);
								added.AddRange(refAdded);
								removed.AddRange(refRemoved);

								var referenceId = reference.Id;
								if (!referenceId.HasValue)
									throw new InvalidOperationException();
								referenceIds.Add(referenceId.Value);
							}

							serializedValue = referenceIds.ToArray();

							if (!hasPropertyData)
								updated = true;
							else
							{
								if (IsReferenceListModified(referenceIds.ToArray(), propertyData))
									updated = true;

								var persistedReferences = ((IEnumerable) propertyData).Cast<object>().Select(i => (int) Convert.ChangeType(i, typeof (int))).ToArray();
								foreach (var priorReference in persistedReferences.Select(id => Fetch(referenceType, id)).Where(r => !references.Contains(r)))
								{
									Type[] refAffectedTypes;
									object[] refAdded;
									object[] refRemoved;

									UpdateData(referenceType, (IJsonEntity)priorReference, true, out refAffectedTypes, out refAdded, out refRemoved);

									affected.AddRange(refAffectedTypes);
									added.AddRange(refAdded);
									removed.AddRange(refRemoved);
								}
							}
						}
						else
						{
							int? referenceId;

							if (value == null)
								referenceId = null;
							else
							{
								Type[] refAffectedTypes;
								object[] refAdded;
								object[] refRemoved;

								UpdateData(referenceType, (IJsonEntity)value, false, out refAffectedTypes, out refAdded, out refRemoved);

								affected.AddRange(refAffectedTypes);
								added.AddRange(refAdded);
								removed.AddRange(refRemoved);

								referenceId = ((IJsonEntity)value).Id;
								if (!referenceId.HasValue)
									throw new InvalidOperationException();
							}

							serializedValue = referenceId;

							if (!hasPropertyData)
								updated = true;
							else
							{
								if (IsReferenceModified(referenceId, propertyData))
									updated = true;

								if (propertyData != null)
								{
									var priorReferenceId = (int?) propertyData;

									if (priorReferenceId != referenceId)
									{
										var priorReference = (IJsonEntity)Fetch(referenceType, priorReferenceId.Value);

										Type[] refAffectedTypes;
										object[] refAdded;
										object[] refRemoved;

										UpdateData(referenceType, priorReference, true, out refAffectedTypes, out refAdded, out refRemoved);

										affected.AddRange(refAffectedTypes);
										added.AddRange(refAdded);
										removed.AddRange(refRemoved);
									}
								}
							}
						}
					}
					else
					{
						serializedValue = value;

						if (!hasPropertyData || IsValueModified(value, propertyData))
							updated = true;
					}

					instanceData[property.Name] = serializedValue;
				}

				if (updated)
					affected.Add(type);
			}

			affectedTypes = affected.ToArray();
			addedEntities = added.ToArray();
			removedEntities = removed.ToArray();
		}

		internal bool IsModified(IJsonEntity entity)
		{
			var type = entity.GetType();
			
			var id = entity.Id;

			if (_deletedEntities.Contains(entity))
			{
				if (!id.HasValue)
					throw new Exception("Accessed deleted entity with no id: " + type.Name + "|.");

				throw new Exception("Accessed deleted entity: " + type.Name + "|" + id + ".");
			}

			if (_pendingDeleteEntities.Contains(entity))
				return true;

			if (_newEntities.Contains(entity))
				return true;

			if (!id.HasValue)
				throw new Exception("Found existing entity with no id: " + type.Name + "|.");

			var typeData = GetTypeData(type);

			Dictionary<string, object> instanceData;
			if (typeData == null || !typeData.TryGetValue(id.Value.ToString(CultureInfo.InvariantCulture), out instanceData))
				throw new Exception("Couldn't find data for existing entity: " + type.Name + "|.");

			foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (property.Name == "Id")
					continue;

				var value = property.GetValue(entity, null);
				
				var propertyType = property.PropertyType;

				object propertyData;
				var hasPropertyData = instanceData.TryGetValue(property.Name, out propertyData);

				Type referenceType;
				bool isReferenceList;
				if (JsonEntity.TryGetReferenceType(propertyType, supportedTypes, out referenceType, out isReferenceList))
				{
					if (isReferenceList)
					{
						var referenceIds = new List<int>();

						foreach (IJsonEntity reference in (IEnumerable) value)
						{
							var referenceId = reference.Id;
							if (!referenceId.HasValue)
								throw new InvalidOperationException();
							referenceIds.Add(referenceId.Value);
						}

						if (!hasPropertyData || IsReferenceListModified(referenceIds.ToArray(), propertyData))
							return true;
					}
					else
					{
						var referenceId = ((IJsonEntity)value).Id;
						if (!referenceId.HasValue)
							throw new InvalidOperationException();

						if (!hasPropertyData || IsReferenceModified(referenceId, propertyData))
							return true;
					}
				}
				else if (!hasPropertyData || IsValueModified(value, propertyData))
					return true;
			}

			return false;
		}
	}
}
