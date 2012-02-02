using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using System.Web.Script.Serialization;
using System.Collections.ObjectModel;
using System.Collections;

namespace ExoModel.UnitTest.JsonModel
{
	/// <summary>
	/// Custom <see cref="IModelTypeProvider"/> implementation that supports defining a model
	/// via JSON type metadata and JSON instance data using dynamic instances.
	/// </summary>
	public class JsonModelTypeProvider : IModelTypeProvider
	{
		public JsonModelTypeProvider()
		{
			this.Model = new JsonModel();
		}

		public JsonModelTypeProvider(string json)
		{
			this.Model = new JsonModel(json);
		}

		public JsonModelTypeProvider Load(string json)
		{
			Model.Load(json);
			return this;
		}

		public JsonModel Model { get; private set; }

		#region IModelTypeProvider

		string IModelTypeProvider.GetModelTypeName(object instance)
		{
			if (instance is JsonInstance)
				return ((IModelInstance)instance).Instance.Type.Name;
			return null;
		}

		string IModelTypeProvider.GetModelTypeName(Type type)
		{
			return null;
		}

		ModelType IModelTypeProvider.CreateModelType(string typeName)
		{
			ModelType type;
			if (Model.Types.TryGetValue(typeName, out type))
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
				return ((JsonModelTypeProvider)Provider).Model.GetInstance(this, id);
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
			internal JsonModelValueProperty(ModelType declaringType, string name, string label, string format, bool isStatic, Type propertyType, bool isList, bool isReadOnly, bool isPersisted)
				: base(declaringType, name, label, format, isStatic, propertyType, null, isList, isReadOnly, isPersisted, null)
			{ }

			protected override object GetValue(object instance)
			{
				throw new NotImplementedException();
			}

			protected override void SetValue(object instance, object value)
			{
				throw new NotImplementedException();
			}
		}

		#endregion

		#region JsonModelReferenceProperty

		internal class JsonModelReferenceProperty : ModelReferenceProperty
		{
			internal JsonModelReferenceProperty(ModelType declaringType, string name, string label, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted)
				: base(declaringType, name, label, format, isStatic, propertyType, isList, isReadOnly, isPersisted, null)
			{ }

			protected override object GetValue(object instance)
			{
				throw new NotImplementedException();
			}

			protected override void SetValue(object instance, object value)
			{
				throw new NotImplementedException();
			}
		}

		#endregion
	}
}
