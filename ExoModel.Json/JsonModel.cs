using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections.ObjectModel;
using System.Collections;

namespace ExoModel.Json
{
	/// <summary>
	/// Represents a complete dynamic model based on JSON type and instance data, based on the JSON
	/// format used by ExoWeb for sending type and instance data to web clients.
	/// </summary>
	public class JsonModel : IModelTypeProvider
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
			if (id != null)
			{
				Dictionary<string, JsonInstance> instances;
				if (Instances.TryGetValue(type, out instances))
				{
					JsonInstance instance;
					if (instances.TryGetValue(id, out instance))
						return instance;
				}

				//the id was not null and could not be found so return a null object
				return null;
			}
			else
			{
				//we need to create a new isntance
				return new JsonInstance(type);
			}
		}

		public Dictionary<string, JsonInstance> GetInstances(ModelType type)
		{
			Dictionary<string, JsonInstance> instances;
			if (Instances.TryGetValue(type, out instances))
				return instances;
			return new Dictionary<string, JsonInstance>();
		}

		public JsonModel Load(string json)
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
						baseType = ModelContext.Current.GetModelType(baseTypeName);
					var format = options.Get<string>("format", null);
					Types.Add(name, new JsonModelType(name, name, baseType, null, format, null));
				}

				// Then load all of the properties
				foreach (var type in types)
				{
					var modelType = Types[type.Key] as JsonModelType;
					var properties = ((Dictionary<string, object>)type.Value).Get<Dictionary<string, object>>("properties", new Dictionary<string, object>()).Select(p =>
					{
						var options = (Dictionary<string, object>)p.Value;
						var typeName = options.Get<string>("type", null);
						var isStatic = options.Get<bool>("isStatic", false);
						var isReadOnly = options.Get<bool>("isReadOnly", false);
						var label = options.Get<string>("label", null);
						var helptext = options.Get<string>("helptext", null);
						var format = options.Get<string>("format", null);
						var isList = options.Get<bool>("isList", false);
						if (typeName.EndsWith("[]"))
						{
							typeName = typeName.Substring(0, typeName.Length - 2);
							isList = true;
						}

						if (valueTypes.ContainsKey(typeName))
							return (ModelProperty)new JsonModelValueProperty(modelType, p.Key, label, helptext, format, isStatic, valueTypes[typeName], isList, isReadOnly, false);
						else
							return (ModelProperty)new JsonModelReferenceProperty(modelType, p.Key, label, helptext, format, isStatic, Types[typeName], isList, isReadOnly, false);
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
					var modelType = ModelContext.Current.GetModelType(type.Key);
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

			return this;
		}

		#region IModelTypeProvider

		string IModelTypeProvider.GetModelTypeName(object instance)
		{
			if (instance is JsonInstance)
				return ((JsonInstance)instance).Type.Name;
			return null;
		}

		string IModelTypeProvider.GetModelTypeName(Type type)
		{
			return null;
		}

		ModelType IModelTypeProvider.CreateModelType(string typeName)
		{
			ModelType type;
			if (Types.TryGetValue(typeName, out type))
				return type;
			return null;
		}

		#endregion

		#region JsonModelType

		internal class JsonModelType : ModelType
		{
			internal JsonModelType(string name, string qualifiedName, ModelType baseType, string scope, string format, Attribute[] attributes)
				: base(name, qualifiedName, baseType, scope, format, attributes)
			{ }

			internal void AddProperties(IEnumerable<ModelProperty> properties)
			{
				foreach (var property in properties)
					AddProperty(property);
			}

			protected override void OnInit()
			{ }

			protected override System.Collections.IList ConvertToList(ModelReferenceProperty property, object list)
			{
				return (IList)list;
			}

			protected override void SaveInstance(ModelInstance modelInstance)
			{
				throw new NotImplementedException();
			}

			public override ModelInstance GetModelInstance(object instance)
			{
				return ((IModelInstance)instance).Instance;
			}

			protected override string GetId(object instance)
			{
				return ((JsonInstance)instance).Id;
			}

			protected override object GetInstance(string id)
			{
				return ((JsonModel)Provider).GetInstance(this, id);
			}

			protected override bool GetIsModified(object instance)
			{
				throw new NotImplementedException();
			}

			protected override bool GetIsDeleted(object instance)
			{
				throw new NotImplementedException();
			}

			protected override bool GetIsPendingDelete(object instance)
			{
				throw new NotImplementedException();
			}

			protected override void SetIsPendingDelete(object instance, bool isPendingDelete)
			{
				throw new NotImplementedException();
			}
		}

		#endregion

		#region JsonModelValueProperty

		internal class JsonModelValueProperty : ModelValueProperty
		{
			internal JsonModelValueProperty(ModelType declaringType, string name, string label, string helptext, string format, bool isStatic, Type propertyType, bool isList, bool isReadOnly, bool isPersisted)
				: base(declaringType, name, label, helptext, format, isStatic, propertyType, null, isList, isReadOnly, isPersisted, null)
			{ }

			protected override object GetValue(object instance)
			{
				return ((JsonInstance)instance)[this];
			}

			protected override void SetValue(object instance, object value)
			{
				((JsonInstance)instance)[this] = value;
			}
		}

		#endregion

		#region JsonModelReferenceProperty

		internal class JsonModelReferenceProperty : ModelReferenceProperty
		{
			internal JsonModelReferenceProperty(ModelType declaringType, string name, string label, string helptext, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted)
				: base(declaringType, name, label, helptext, format, isStatic, propertyType, isList, isReadOnly, isPersisted, null)
			{ }

			protected override object GetValue(object instance)
			{
				return ((JsonInstance)instance)[this];
			}

			protected override void SetValue(object instance, object value)
			{
				((JsonInstance)instance)[this] = value;
			}
		}

		#endregion
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
