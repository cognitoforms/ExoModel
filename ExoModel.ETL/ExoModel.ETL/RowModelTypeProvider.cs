using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections;
using ExoModel;
using System.Text.RegularExpressions;

namespace ExoModel.ETL
{
	/// <summary>
	/// Represents a complete dynamic model based on Row type and instance data, based on the Row
	/// format used by ExoWeb for sending type and instance data to web clients.
	/// </summary>
	public class RowModelTypeProvider : IModelTypeProvider, IRowTypeProvider
	{
		Dictionary<ModelType, Dictionary<string, RowInstance>> Instances { get; set; }
		public Dictionary<string, ModelType> Types { get; private set; }
		private ITranslator Translator { get; set; }

		/// <summary>
		/// Initialize provider with default values.
		/// </summary>
		public RowModelTypeProvider(ITranslator translator)
		{
			this.Instances = new Dictionary<ModelType, Dictionary<string, RowInstance>>();
			this.Types = new Dictionary<string, ModelType>();
			this.Translator = translator;
		}

		/// <summary>
		/// This function is responsible for build a new dynamic type
		/// from the column header information provided.  It will also
		/// handle converting any invalid property names into
		/// valid names and keeping track of the corresponding mapping.
		/// </summary>
		/// <param name="columnHeaders">A list of strings that represent the column headers of the row based file.</param>
		/// <param name="dynamicType">The new dynamically generated type.</param>
		/// <returns></returns>
		public IRowTypeProvider CreateType(IEnumerable<string> columnHeaders, out ModelType dynamicType, string typeName)
		{
			//generate a name for the dyanmic type
			string dynamicTypeName = typeName == null || Types.ContainsKey(typeName) ? GenerateUniqueTypeName() : typeName;

			if (ModelContext.Current.GetModelType(dynamicTypeName) == null)
			{
				dynamicType = new RowModelType(dynamicTypeName, dynamicTypeName, null, null, null, null);

				this.Types.Add(dynamicType.Name, dynamicType);

				IEnumerable<ModelProperty> typeProperties = BuildPropertiesList(dynamicType, columnHeaders);
				((RowModelType)dynamicType).AddProperties(typeProperties);
			}
			else
			{
				dynamicType = ModelContext.Current.GetModelType(dynamicTypeName);
			}

			return this;
		}

		/// <summary>
		/// Creates a new row instance based on the data provided and stores it 
		/// in the providers collection of instances.  The first object in Row
		/// should be the id of the new object to create.
		/// </summary>
		/// <param name="type">The type generated using the LoadType function</param>
		/// <param name="Row">The data needed to instantiate the new RowInstance</param>
		/// <returns></returns>
		public IRowTypeProvider CreateInstance(ModelType type, IEnumerable<object> Row)
		{
			RowInstance instance = new RowInstance(type, Row.First().ToString());
			int index = 0;

			//the instances values are in order
			//the first value should be the record id
			foreach (var value in Row)
			{
				var newValue = value;
				ModelProperty property = type.Properties.ElementAt(index);

				if (newValue == null)
					continue;

				var valueType = ((ModelValueProperty)property).PropertyType;
				if (valueType == typeof(DateTime))
					newValue = DateTime.Parse((string)value);

				instance[property] = newValue;

				index++;
			}

			if(Instances.ContainsKey(type))
			{
				if (!this.Instances[type].ContainsKey(instance.Id))
				{
					this.Instances[type].Add(instance.Id, instance);
				}
				else
				{
					throw new Exception("An instance of type " + type.Name + " already exist with an id of " + instance.Id);
				}
			}
			else
			{
				var tempDict = new Dictionary<string,RowInstance>();
				tempDict.Add(instance.Id, instance);
				this.Instances.Add(type, tempDict);	
			}
			
			return this;
		}

		public ModelInstance GetModelInstance(ModelType type, string id)
		{
			var temp = GetInstance(type, id);
			if (temp != null)
				return ModelContext.Current.GetModelInstance(temp);
			else
				return null;
		}

		#region Helpers
		/// <summary>
		/// Helper method to retrieve an instance of type based id
		/// </summary>
		/// <param name="type">The type of object you are searching for in the provider</param>
		/// <param name="id">The id of the object you are searching for in the provider.</param>
		/// <returns></returns>
		public RowInstance GetInstance(ModelType type, string id)
		{
			if (id != null)
			{
				Dictionary<string, RowInstance> instances;
				if (Instances.TryGetValue(type, out instances))
				{
					RowInstance instance;
					if (instances.TryGetValue(id, out instance))
						return instance;
				}

				return null;
			}
			else
			{
				//create a new instance
				return new RowInstance(type);
			}
		}

		/// <summary>
		/// Retrieve all instances in the provider of a certain type.
		/// </summary>
		/// <param name="type">The type of objects you are searching for.</param>
		/// <returns></returns>
		public Dictionary<string, RowInstance> GetInstances(ModelType type)
		{
			Dictionary<string, RowInstance> instances;
			if (Instances.TryGetValue(type, out instances))
				return instances;
			return new Dictionary<string, RowInstance>();
		}

		/// <summary>
		/// Retrieve all instances in the provider of a certain type.
		/// </summary>
		/// <param name="type">The type of objects you are searching for.</param>
		/// <returns></returns>
		public IEnumerable<ModelInstance> GetModelInstances(ModelType type)
		{
			IList<ModelInstance> retInstances = new List<ModelInstance>();
			Dictionary<string, RowInstance> localInstances;

			if (Instances.TryGetValue(type, out localInstances))
			{
				foreach (KeyValuePair<string, RowInstance> instance in localInstances)
				{
					retInstances.Add(ModelContext.Current.GetModelInstance(instance.Value));
				}
			}

			return retInstances;
		}
		
		/// <summary>
		/// Builds a list of of valid property names for the newly created ModelType.
		/// </summary>
		/// <param name="type">The dynamically created type.</param>
		/// <param name="columnHeaders">A list of property names to add to the type.</param>
		/// <returns></returns>
		private IEnumerable<ModelProperty> BuildPropertiesList(ModelType type, IEnumerable<string> columnHeaders)
		{
			IList<RowModelValueProperty> builtProperties = new List<RowModelValueProperty>();
			foreach (string propName in columnHeaders)
			{
				string translatedName = Translator.AddTranslation(propName);
				builtProperties.Add(new RowModelValueProperty(type, translatedName, null, null, false, typeof(string), false, false, false));
			}

			return builtProperties;
		}

		/// <summary>
		/// Generates a new type name that will be guaranteed to be unique.
		/// </summary>
		/// <returns>A unique type name to be utilized for the new dynamic type.</returns>
		private string GenerateUniqueTypeName()
		{
			return "_" + Guid.NewGuid().ToString();
		}
		#endregion

		#region IModelTypeProvider

		string IModelTypeProvider.GetModelTypeName(object instance)
		{
			if (instance is RowInstance)
				return ((RowInstance)instance).Type.Name;
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

		#region RowModelType

		internal class RowModelType : ModelType
		{
			internal RowModelType(string name, string qualifiedName, ModelType baseType, string scope, string format, Attribute[] attributes)
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
				return ((RowInstance)instance).Id;
			}

			protected override object GetInstance(string id)
			{
				return ((RowModelTypeProvider)Provider).GetInstance(this, id);
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

		#region RowModelValueProperty

		internal class RowModelValueProperty : ModelValueProperty
		{
			internal RowModelValueProperty(ModelType declaringType, string name, string label, string format, bool isStatic, Type propertyType, bool isList, bool isReadOnly, bool isPersisted)
				: base(declaringType, name, label, format, isStatic, propertyType, null, isList, isReadOnly, isPersisted, null)
			{ }

			protected override object GetValue(object instance)
			{
				return ((RowInstance)instance)[this];
			}

			protected override void SetValue(object instance, object value)
			{
				((RowInstance)instance)[this] = value;
			}
		}

		#endregion

		#region RowModelReferenceProperty

		internal class RowModelReferenceProperty : ModelReferenceProperty
		{
			internal RowModelReferenceProperty(ModelType declaringType, string name, string label, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted)
				: base(declaringType, name, label, format, isStatic, propertyType, isList, isReadOnly, isPersisted, null)
			{ }

			protected override object GetValue(object instance)
			{
				return ((RowInstance)instance)[this];
			}

			protected override void SetValue(object instance, object value)
			{
				((RowInstance)instance)[this] = value;
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
