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
		RowModelType rootType;
		Dictionary<string, RowModelType> types = new Dictionary<string, RowModelType>();
		Regex nameRegex = new Regex(@"[^a-zA-Z0-9]", RegexOptions.Compiled);
		string @namespace;

		/// <summary>
		/// Initialize provider with default values.
		/// </summary>
		public RowModelTypeProvider(string @namespace, ITable table, string identifier)
		{
			this.@namespace = @namespace;

			// Create model types based on the specified root table
			this.rootType = CreateType(table, identifier, null);

			// Create model instances based on the specified root table
			CreateInstances(table, this.rootType);
		}

		/// <summary>
		/// Recursively processes <see cref="ITable"/> instances to build a corresponding <see cref="RowModelType"/> hierarchy.
		/// </summary>
		/// <param name="table"></param>
		/// <param name="identifier"></param>
		/// <param name="parentType"></param>
		RowModelType CreateType(ITable table, string identifier, RowModelType parentType)
		{
			// Ensure the table has at least two columns
			if (table.Columns.Count() < 2)
				throw new ArgumentException("The import table '" + table.Name + "' only has " + table.Columns.Count() + " column, and at least 2 are required.");

			// Determine the type name
			var typeName = String.IsNullOrEmpty(@namespace) ? table.Name : @namespace + "." + table.Name;
			var type = new RowModelType(typeName, parentType);
			this.types[typeName] = type;

			// Create properties for each column in the table
			var properties = table.Columns.Select(column =>
				(ModelProperty)new RowModelValueProperty(type, nameRegex.Replace(column.Name, ""), column.Name, null, null, false, typeof(string), false, false, false)).ToList();

			// Establish the relationship between the parent and child if a parent type was specified
			if (parentType != null)
			{
				// Create the reference from the child to the parent
				var parentColumn = properties[1];
				properties[1] = new RowModelReferenceProperty(type, parentColumn.Name, parentColumn.Label, null, null, false, parentType, false, false, true);

				// Create the reference from the parent to the child
				parentType.AddProperties(new[] { new RowModelReferenceProperty(parentType, nameRegex.Replace(type.Name, ""), type.Name, null, null, false, type, true, false, true) });
			}

			// Add the properties to the model type
			type.AddProperties(properties);

			// Replace property labels with property names in mapping expressions
			foreach (var property in properties)
				identifier = identifier
					.Replace("[" + property.Label + "]", property.Name)
					.Replace(property.Label, property.Name);

			// Compile the identifier expression
			type.IdentifierExpression = type.GetExpression(identifier);

			// Recursively create child types
			foreach (var child in table.Children)
				CreateType(child, child.Columns.First().Name, type);

			// Return the new model type
			return type;
		}

		/// <summary>
		/// Recursively processes <see cref="ITable"/> instances to create <see cref="RowInstance"/> data for each table in the hierarchy.
		/// </summary>
		/// <param name="table"></param>
		void CreateInstances(ITable table, RowModelType type)
		{
			// Get the properties for the specified table columns
			var properties = table.Columns.Select(c => type.Properties[c.Name] as ModelValueProperty).ToArray();

			// Verify all of the columns correlate to value properties
			if (properties.Any(p => p == null))
				throw new ArgumentException("The specified table columns do not match properties on the specified model type.");

			// Create instances for each row in the table
			foreach (var row in table.Rows)
			{
				var instance = new RowInstance(type);
				var c = 0;
				foreach (var value in row)
					instance.SetValue(properties[c++], value);
				type.Instances.Add(instance.Id, instance);
			}

			// Recursively process child tables
			foreach (var child in table.Children)
				CreateInstances(child, (RowModelType)((RowModelReferenceProperty)type.Properties[child.Name]).PropertyType);
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
		/// Retrieve all instances in the provider of a certain type.
		/// </summary>
		/// <param name="type">The type of objects you are searching for.</param>
		/// <returns></returns>
		public Dictionary<string, RowInstance> GetInstances(ModelType type)
		{
			return ((RowModelType)type).Instances;
		}

		/// <summary>
		/// Retrieve all instances in the provider of a certain type.
		/// </summary>
		/// <param name="type">The type of objects you are searching for.</param>
		/// <returns></returns>
		public IEnumerable<ModelInstance> GetModelInstances(ModelType type)
		{
			return ((RowModelType)type).Instances.Values.Select(i => ((IModelInstance)i).Instance);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public int GetCount(ModelType type)
		{
			return ((RowModelType)type).Instances.Count;
		}

		#region IModelTypeProvider

		bool IModelTypeProvider.IsCachable { get { return false; } }

		string IModelTypeProvider.Namespace { get { return @namespace; } }

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
			RowModelType type;
			types.TryGetValue(typeName, out type);
			return type;
		}

		#endregion


	}

	#region RowModelType

	public class RowModelType : ModelType, ITable
	{
		public RowModelType(string name, RowModelType parentType)
			: base(name, name, null, null, null, null)
		{
			this.ParentType = parentType;
			this.Instances = new Dictionary<string, RowInstance>();
		}

		public void AddValueProperty(string name, Type propertyType = null, string format = null)
		{
			AddProperty(new RowModelValueProperty(this, name, name, null, format, false, propertyType ?? typeof(string), false, false, true));
		}

		public void AddCalculatedProperty<T>(string name, Func<RowInstance, T> calculation)
		{
			AddProperty(new RowModelCalculatedProperty(this, name, typeof(T), i => calculation(i)));
		}

		public void AddCalculatedProperty(string name, Type propertyType, string expression)
		{
			AddProperty(new RowModelCalculatedProperty(this, name, propertyType, expression));
		}

		public void AddParentProperty(string name, int level)
		{
			AddProperty(new RowModelParentIdProperty(this, name, level));
		}

		public void AddReferenceProperty(string name, RowModelType type, bool isList)
		{
			AddProperty(new RowModelReferenceProperty(this, name, name, null, null, false, type, isList, false, true));
		}

		internal void AddProperties(IEnumerable<ModelProperty> properties)
		{
			foreach (var property in properties)
				AddProperty(property);
		}

		public ModelExpression IdentifierExpression { get; set; }

		public ModelExpression ParentIdentifierExpression { get; set; }

		public RowModelType ParentType { get; private set; }

		internal Dictionary<string, RowInstance> Instances { get; private set; }

		protected override void OnInit()
		{
			// Initialize calculated properties
			foreach (var property in Properties.OfType<RowModelCalculatedProperty>())
				property.Initialize();
		}

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
			return IdentifierExpression.Invoke((ModelInstance)instance).ToString();
		}

		protected override object GetInstance(string id)
		{
			// Get the existing instance if an identifer was specified
			if (id != null)
			{
				RowInstance instance;
				Instances.TryGetValue(id, out instance);
				if (instance == null)
					Instances[id] = instance = new RowInstance(this);
				return instance;
			}

			// Otherwise, create a new instance
			else
				return new RowInstance(this);
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

		string ITable.Name
		{
			get { return Name; }
		}

		string ITable.Identifier
		{
			get { return IdentifierExpression.Expression.ToString(); }
		}

		ITable ITable.Parent
		{
			get { return ParentType; }
		}

		string ITable.ParentIdentifier
		{
			get { return ParentIdentifierExpression == null ? null : ParentIdentifierExpression.Expression.ToString(); }
		}

		IEnumerable<ITable> ITable.Children
		{
			get 
			{ 
				return 
					Properties
						.OfType<RowModelReferenceProperty>()
						.Where(p => p.IsList)
						.Select(p => (RowModelType)p.PropertyType); 
			}
		}

		IEnumerable<Column> ITable.Columns
		{
			get { return Properties.OfType<ModelValueProperty>().Select(p => new Column(p.Name, p.PropertyType, p.Format)); }
		}

		IEnumerable<IEnumerable<string>> ITable.Rows
		{
			get { return Instances.Values.Select(i => i.Values); }
		}

		void IDisposable.Dispose()
		{ }
	}

	#endregion

	#region RowModelValueProperty

	internal class RowModelValueProperty : ModelValueProperty
	{
		internal RowModelValueProperty(ModelType declaringType, string name, string label, string helptext, string format, bool isStatic, Type propertyType, bool isList, bool isReadOnly, bool isPersisted)
			: base(declaringType, name, label, helptext, format, isStatic, propertyType, null, isList, isReadOnly, isPersisted, null)
		{ }

		protected override object GetValue(object instance)
		{
			return ((RowInstance)instance)[this.Index];
		}

		protected override void SetValue(object instance, object value)
		{
			((RowInstance)instance)[this.Index] = value;
		}
	}

	#endregion

	#region RowModelCalculatedProperty

	/// <summary>
	/// Represents properties that are calculated based on delegates and model expressions.
	/// </summary>
	internal class RowModelCalculatedProperty : ModelValueProperty
	{
		string expression;
		Func<RowInstance, object> calculation;

		internal RowModelCalculatedProperty(ModelType declaringType, string name, Type propertyType, Func<RowInstance, object> calculation)
			: base(declaringType, name, name, null, null, false, propertyType, null, false, true, false, null)
		{
			this.calculation = calculation;
		}

		internal RowModelCalculatedProperty(ModelType declaringType, string name, Type propertyType, string expression)
			: base(declaringType, name, name, null, null, false, propertyType, null, false, true, false, null)
		{
			this.expression = expression;
		}

		internal void Initialize()
		{
			if (expression != null)
			{
				var modelExpression = DeclaringType.GetExpression(PropertyType, expression);
				
				// Expressions that do not require root instances (like static properties or constant expressions)
				if (modelExpression.CompiledExpression.Method.GetParameters().Length == 0)
					calculation = i => modelExpression.CompiledExpression.DynamicInvoke();

				// Expressions requiring a root instance
				else
					calculation = i => modelExpression.CompiledExpression.DynamicInvoke(i);
			}
		}

		protected override object GetValue(object instance)
		{
			return calculation((RowInstance)instance);
		}

		protected override void SetValue(object instance, object value)
		{
			throw new NotSupportedException("Calculated row model properties cannot be set.");
		}
	}

	#endregion

	#region RowModelParentIdProperty

	internal class RowModelParentIdProperty : ModelValueProperty
	{
		int level;

		internal RowModelParentIdProperty(ModelType declaringType, string name, int level)
			: base(declaringType, name, name, null, null, false, typeof(string), null)
		{
			this.level = level;
		}

		protected override object GetValue(object instance)
		{
			var depth = level;
			var parent = (RowInstance)instance;
			while (depth-- > 0 && parent != null)
				parent = parent.Parent;
			if (parent == null)
				return null;
			return parent.Id;
		}

		protected override void SetValue(object instance, object value)
		{
			throw new NotSupportedException("Parent Id properties cannot be set.");
		}
	}

	#endregion

	#region RowModelReferenceProperty

	internal class RowModelReferenceProperty : ModelReferenceProperty
	{
		internal RowModelReferenceProperty(ModelType declaringType, string name, string label, string helptext, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted)
			: base(declaringType, name, label, helptext, format, isStatic, propertyType, isList, isReadOnly, isPersisted, null)
		{ }

		protected override object GetValue(object instance)
		{
			return ((RowInstance)instance)[this.Index];
		}

		protected override void SetValue(object instance, object value)
		{
			((RowInstance)instance)[this.Index] = value;
		}
	}

	#endregion

	#region DictionaryHelper

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

	#endregion
}
