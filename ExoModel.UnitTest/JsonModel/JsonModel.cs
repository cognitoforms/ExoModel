using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections.ObjectModel;
using System.Collections;

namespace ExoModel.UnitTest.JsonModel
{
	/// <summary>
	/// Represents a complete dynamic model based on JSON type and instance data, based on the JSON
	/// format used by ExoWeb for sending type and instance data to web clients.
	/// </summary>
	public class JsonModel
	{
		static Dictionary<string, Type> valueTypes = new Dictionary<string, Type>() 
			{ 
				{ "String", typeof(string) }, { "Number", typeof(decimal) }, { "Boolean", typeof(bool) }, { "Date", typeof(DateTime) }
			};

		public JsonModel()
		{
			this.Types = new Dictionary<string, ModelType>();
			this.Instances = new Dictionary<ModelType, Dictionary<string, JsonInstance>>();
		}

		public JsonModel(string json)
			: this()
		{
			Load(json);
		}

		public Dictionary<string, ModelType> Types { get; private set; }

		Dictionary<ModelType, Dictionary<string, JsonInstance>> Instances { get; set; }

		public JsonInstance GetInstance(ModelType type, string id)
		{
			Dictionary<string, JsonInstance> instances;
			if (Instances.TryGetValue(type, out instances))
			{
				JsonInstance instance;
				if (instances.TryGetValue(id, out instance))
					return instance;
			}
			return null;
		}

		public void Load(string json)
		{
			// Parse the json into a parse tree
			var meta = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);

			// Load model types
			var types = meta.Get<Dictionary<string, object>>("types", null);
			if (types != null)
			{
				// Create all of the model types
				foreach (var type in types)
				{
					var name = type.Key;
					var options = (Dictionary<string, object>)type.Value;
					var baseTypeName = options.Get<string>("baseType", null);
					ModelType baseType = null;
					if (baseTypeName != null)
						Types.TryGetValue(baseTypeName, out baseType);
					var format = options.Get<string>("format", null);
					Types.Add(name, new JsonModelTypeProvider.JsonModelType(name, name, baseType, null, format, null));
				}

				// Then load all of the properties
				foreach (var type in types)
				{
					var modelType = Types[type.Key] as JsonModelTypeProvider.JsonModelType;
					var properties = ((Dictionary<string, object>)type.Value).Get<Dictionary<string, object>>("properties", new Dictionary<string, object>()).Select(p =>
					{
						var options = (Dictionary<string, object>)p.Value;
						var typeName = options.Get<string>("type", null);
						var isStatic = options.Get<bool>("isStatic", false);
						var isReadOnly = options.Get<bool>("isReadOnly", false);
						var label = options.Get<string>("label", null);
						var format = options.Get<string>("format", null);
						var isList = options.Get<bool>("isList", false);
						if (typeName.EndsWith("[]"))
						{
							typeName = typeName.Substring(0, typeName.Length - 2);
							isList = true;
						}

						if (valueTypes.ContainsKey(typeName))
							return (ModelProperty)new JsonModelTypeProvider.JsonModelValueProperty(modelType, p.Key, label, format, isStatic, valueTypes[typeName], isList, isReadOnly, false);
						else
							return (ModelProperty)new JsonModelTypeProvider.JsonModelReferenceProperty(modelType, p.Key, label, format, isStatic, Types[typeName], isList, isReadOnly, false);
					})
					.ToList();

					// Initialize properties as part of the type initialization process
					modelType.AfterInitialize(() =>
					{
						// Add base type instance properties first
						if (modelType.BaseType != null)
							modelType.AddProperties(modelType.BaseType.Properties.Where(p => !p.IsStatic));

						// Then add instance properties
						modelType.AddProperties(properties.Where(p => !p.IsStatic));

						// Finally add static properties
						modelType.AddProperties(properties.Where(p => p.IsStatic));
					});
				}
			}

			// Load model instances
			var typeInstances = meta.Get<Dictionary<string, object>>("instances", null);
			if (typeInstances != null)
			{
				// First build a strongly-typed parse tree from the JSON instance data
				var instances = typeInstances.Select(type =>
				{
					var modelType = Types[type.Key];
					var instanceMeta = (Dictionary<string, object>)type.Value;

					return new
					{
						Type = modelType,
						StaticProperties = (object)null,
						Cache = Instances.ContainsKey(modelType) ? Instances[modelType] : Instances[modelType] = new Dictionary<string,JsonInstance>(),
						Instances = instanceMeta.Where(instance => instance.Key != "static").Select(instance => new
						{
							Instance = new JsonInstance(modelType, instance.Key),
							Properties = (ArrayList)instance.Value
						})
						.ToList()
					};
				})
				.ToList();

				// Then add the instances to the model cache
				foreach (var type in instances)
				{
					foreach (var instance in type.Instances)
						type.Cache[instance.Instance.Id] = instance.Instance;
				}

				// Finally initialize properties for each instance
				foreach (var type in instances)
				{
					foreach (var instance in type.Instances)
					{
						foreach (var property in type.Type.Properties)
						{
							if (property.IsStatic)
								continue;

							var value = instance.Properties[property.Index];
							if (value == null)
								continue;

							// Value
							if (property is ModelValueProperty)
							{
								var valueType = ((ModelValueProperty)property).PropertyType;
								if (valueType == typeof(DateTime))
									value = DateTime.Parse((string)value);
								instance.Instance[property] = value;
							}

							// List
							else if (property.IsList)
							{
								var listType = ((ModelReferenceProperty)property).PropertyType;
								instance.Instance[property] = new ObservableCollection<JsonInstance>(((ArrayList)value).Cast<string>().Select(id => GetInstance(listType, id)));
							}

							// Instance
							else
								instance.Instance[property] = GetInstance(((ModelReferenceProperty)property).PropertyType, (string)value);
						}
					}
				}
			}
		}
	}

	/// <summary>
	/// Utility class to simplify access to string/object dictionaries.
	/// </summary>
	internal static class DictionaryHelper
	{
		internal static T Get<T>(this Dictionary<string, object> dictionary, string name, T defaultValue)
		{
			object value;
			if (dictionary.TryGetValue(name, out value) && value is T)
				return (T)value;
			return defaultValue;
		}
	}
}
