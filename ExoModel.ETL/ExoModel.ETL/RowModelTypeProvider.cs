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
	/// Represents a complete dynamic model based on a tabular import file.
	/// </summary>
	public class RowModelTypeProvider : IModelTypeProvider
	{
		Dictionary<string, ModelType> types = new Dictionary<string, ModelType>();
		Dictionary<ModelType, Dictionary<string, RowInstance>> instances = new Dictionary<ModelType, Dictionary<string, RowInstance>>();
		Regex nameRegex = new Regex(@"[^a-zA-Z0-9]", RegexOptions.Compiled);

		/// <summary>
		/// Initialize provider with default values.
		/// </summary>
		public RowModelTypeProvider(string @namespace, ITabularImportFile data, string identifierExpression)
		{
			// Create types for each table in the data set
			foreach (string table in data.GetTableNames())
			{
				var typeName = String.IsNullOrEmpty(@namespace) ? table : @namespace + "." + table;
				var type = new RowModelType(typeName, typeName, null, null, null, null);
				this.types[typeName] = type;

				// Create properties for each column in the table
				type.AddProperties(data.GetColumnNames(table).Select(column =>
					new RowModelValueProperty(type, nameRegex.Replace(column, ""), column, null, false, typeof(string), false, false, false)));

				// Replace property labels with property names in mapping expressions
				foreach (var property in type.Properties)
					identifierExpression = identifierExpression
						.Replace("[" + property.Label + "]", property.Name)
						.Replace(property.Label, property.Name);

				type.IdentifierExpression = type.GetExpression(identifierExpression);

				// Create instances for each row in the table
				instances[type] = data.GetRows(table).ToDictionary(r => r[0], r => new RowInstance(type, r));
			}
		}

		/// <summary>
		/// Gets the <see cref="ModelType"/> instances represented by the type provider.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<ModelType> GetTypes()
		{
			return types.Values;
		}

		/// <summary>
		/// Gets the module instance for the specified <see cref="ModelType"/> and identifier.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		public ModelInstance GetModelInstance(ModelType type, string id)
		{
			var instance = GetInstance(type, id);
			if (instance != null)
				return ((IModelInstance)instance).Instance;
			else
				return null;
		}

		/// <summary>
		/// Helper method to retrieve an instance of type based id
		/// </summary>
		/// <param name="type">The type of object you are searching for in the provider</param>
		/// <param name="id">The id of the object you are searching for in the provider.</param>
		/// <returns></returns>
		public RowInstance GetInstance(ModelType type, string id)
		{
			// Get the existing instance if an identifer was specified
			if (id != null) {
				RowInstance instance;
				GetInstances(type).TryGetValue(id, out instance);
				return instance;
			}

			// Otherwise, create a new instance
			else
				return new RowInstance(type);
		}

		/// <summary>
		/// Retrieve all instances in the provider of a certain type.
		/// </summary>
		/// <param name="type">The type of objects you are searching for.</param>
		/// <returns></returns>
		public Dictionary<string, RowInstance> GetInstances(ModelType type)
		{
			Dictionary<string, RowInstance> typeInstances;
			if (instances.TryGetValue(type, out typeInstances))
				return typeInstances;
			return new Dictionary<string, RowInstance>();
		}

		/// <summary>
		/// Retrieve all instances in the provider of a certain type.
		/// </summary>
		/// <param name="type">The type of objects you are searching for.</param>
		/// <returns></returns>
		public IEnumerable<ModelInstance> GetModelInstances(ModelType type)
		{
			Dictionary<string, RowInstance> typeInstances;
			if (instances.TryGetValue(type, out typeInstances))
				return typeInstances.Values.Select(i => ((IModelInstance)i).Instance);

			return new ModelInstance[0];
		}

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
			types.TryGetValue(typeName, out type);
			return type;
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

			internal ModelExpression IdentifierExpression { get; set; }

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
				return (ModelInstance)instance;
			}

			protected override string GetId(object instance)
			{
				return IdentifierExpression.Expression.Parameters.Count == 0 ?
								IdentifierExpression.CompiledExpression.DynamicInvoke().ToString() :
								IdentifierExpression.CompiledExpression.DynamicInvoke(instance).ToString();
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
