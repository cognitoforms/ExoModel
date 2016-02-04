using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ExoModel.Json
{
	public class JsonModelTypeProvider : ReflectionModelTypeProvider
	{
		internal JsonModelTypeProvider(Assembly @assembly, JsonEntityContext entityContext)
			: base(null, JsonModel.GetEntityTypes(@assembly), null, true)
		{
			EntityContext = entityContext;
		}

		internal JsonModelTypeProvider(Assembly @assembly, string @namespace, JsonEntityContext entityContext)
			: base(@namespace, JsonModel.GetEntityTypes(@assembly, @namespace), null, true)
		{
			EntityContext = entityContext;
		}

		internal JsonEntityContext EntityContext { get; private set; }

		protected override ReflectionModelType CreateModelType(string @namespace, Type type, string format)
		{
			return new JsonModelType(EntityContext, @namespace, type, "", format);
		}

		protected override ModelReferenceProperty CreateReferenceProperty(ModelType declaringType, PropertyInfo property, string name, string label, string helptext, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
		{
			return new JsonReferenceProperty(declaringType, property, name, label, helptext, format, isStatic, propertyType, isList, isReadOnly, isPersisted, attributes);
		}

		protected override ModelValueProperty CreateValueProperty(ModelType declaringType, PropertyInfo property, string name, string label, string helptext, string format, bool isStatic, Type propertyType, TypeConverter converter, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
		{
			return new JsonValueProperty(declaringType, property, name, label, helptext, isStatic, propertyType, converter, isList, isReadOnly, isPersisted, attributes);
		}

		public class JsonModelType : ReflectionModelType
		{
			private readonly JsonEntityContext _entityContext;
			private readonly Type _type;

			private PropertyInfo _idProperty;

			internal JsonModelType(JsonEntityContext entityContext, string @namespace, Type type, string scope, string format)
				: base(@namespace, type, scope, format)
			{
				this._entityContext = entityContext;
				this._type = type;
			}

			private PropertyInfo IdProperty
			{
				get
				{
					if (_idProperty == null)
						_idProperty = _type.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);

					return _idProperty;
				}
			}

			protected override void SaveInstance(ModelInstance modelInstance)
			{
				if (_entityContext.Save((IJsonEntity)modelInstance.Instance))
					OnSave(modelInstance);
			}

			protected override string GetId(object instance)
			{
				var value = IdProperty.GetValue(instance, null);

				if (value == null)
					return null;

				return Convert.ToString(value);
			}

			protected override object GetInstance(string id)
			{
				if (string.IsNullOrEmpty(id))
					return JsonEntity.CreateNew(_type);

				return _entityContext.Fetch(_type, int.Parse(id));
			}

			protected override bool GetIsModified(object instance)
			{
				return _entityContext.IsModified((IJsonEntity)instance);
			}

			protected override bool GetIsDeleted(object instance)
			{
				return _entityContext.IsDeleted((IJsonEntity)instance);
			}

			protected override bool GetIsPendingDelete(object instance)
			{
				return _entityContext.IsPendingDelete((IJsonEntity)instance);
			}

			protected override void SetIsPendingDelete(object instance, bool isPendingDelete)
			{
				_entityContext.SetPendingDelete((IJsonEntity)instance, isPendingDelete);
			}
		}

		public class JsonReferenceProperty : ReflectionReferenceProperty
		{
			private readonly DisplayAttribute displayAttribute;

			public JsonReferenceProperty(ModelType declaringType, PropertyInfo property, string name, string label, string helptext, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
				: base(declaringType, property, name, label, helptext, format, isStatic, propertyType, isList, isReadOnly, isPersisted, attributes)
			{
				displayAttribute = GetAttributes<DisplayAttribute>().FirstOrDefault();
			}

			/// <summary>
			/// Gets the localized label for properties that have a <see cref="DisplayAttribute"/>.
			/// </summary>
			public override string Label
			{
				get
				{
					return displayAttribute != null ? displayAttribute.GetName() : base.Label;
				}
			}
		}

		public class JsonValueProperty : ReflectionValueProperty
		{
			private readonly DisplayAttribute displayAttribute;

			public JsonValueProperty(ModelType declaringType, PropertyInfo property, string name, string label, string helptext, bool isStatic, Type propertyType, TypeConverter converter, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes, LambdaExpression defaultValue = null)
				: base(declaringType, property, name, label, helptext, GetFormat(propertyType, attributes), isStatic, propertyType, converter, isList, isReadOnly, isPersisted, attributes, defaultValue)
			{
				displayAttribute = GetAttributes<DisplayAttribute>().FirstOrDefault();
			}

			/// <summary>
			/// Gets the localized label for properties that have a <see cref="DisplayAttribute"/>.
			/// </summary>
			public override string Label
			{
				get
				{
					return displayAttribute != null ? displayAttribute.GetName() : base.Label;
				}
			}

			private static string GetFormat(Type propertyType, IEnumerable<Attribute> attributes)
			{
				string format;

				var formatAttr = attributes.OfType<DisplayFormatAttribute>().FirstOrDefault();
				if (formatAttr != null)
					format = formatAttr.DataFormatString;
				else if (propertyType == typeof (int))
					format = "N0";
				else
					format = null;

				return format;
			}
		}
	}
}
