using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace ExoModel
{
	/// <summary>
	/// Implementation of <see cref="DynamicModelTypeProvider"/> that uses <see cref="ICustomTypeDescriptor"/>
	/// implementations to expose dynamic characteristics of a concrete instance.  This type provider should be used
	/// in cases where classes already implement <see cref="ICustomTypeDescriptor"/> to exposes dynamic properties 
	/// and it is preferred to leverage the existing implementation instead of explicitly implementing 
	/// <see cref="DynamicModelTypeProvider"/>.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class DescriptorModelTypeProvider<T> : DynamicModelTypeProvider<ICustomTypeDescriptor, PropertyDescriptor>
		where T : class, ICustomTypeDescriptor
	{
		#region Fields

		Func<string, T> create;
		Func<ModelInstance, string> getScopeName;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="DescriptorModelTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="namespace"></param>
		/// <param name="create"></param>
		public DescriptorModelTypeProvider(string @namespace, Func<string, T> create, Func<ModelInstance, string> getScopeName)
			: base(@namespace, ModelContext.Current.GetModelType<T>().Name)
		{
			this.create = create;
			this.getScopeName = getScopeName;
		}

		#endregion

		#region Methods

		protected override object CreateInstance(ICustomTypeDescriptor type)
		{
			return create(type.GetClassName());
		}

		protected override IEnumerable<PropertyDescriptor> GetProperties(ICustomTypeDescriptor type)
		{
			return type.GetProperties().Cast<PropertyDescriptor>();
		}

		protected override ICustomTypeDescriptor GetTypeSource(string typeName)
		{
			return create(typeName);
		}

		protected override ICustomTypeDescriptor GetTypeSource(object instance)
		{
			return (ICustomTypeDescriptor)instance;
		}

		protected override Attribute[] GetTypeAttributes(ICustomTypeDescriptor type)
		{
			return type.GetAttributes().Cast<Attribute>().ToArray();
		}

		protected override string GetClassName(ICustomTypeDescriptor instance)
		{
			return instance.GetClassName();
		}

		protected override string GetPropertyName(PropertyDescriptor property)
		{
			return property.Name;
		}

		protected override Attribute[] GetPropertyAttributes(PropertyDescriptor property)
		{
			return property.Attributes.Cast<Attribute>().ToArray();
		}

		protected override bool IsList(PropertyDescriptor property)
		{
			Type itemType;
			ModelContext.Current.GetModelType<T>().TryGetListItemType(property.PropertyType, out itemType);
			return itemType != null;
		}

		protected override bool IsStatic(PropertyDescriptor property)
		{
			return false;
		}

		protected override bool IsReadOnly(PropertyDescriptor property)
		{
			return property.IsReadOnly;
		}

		protected override bool IsPersisted(PropertyDescriptor property)
		{
			return true;
		}

		protected override ModelType GetReferenceType(PropertyDescriptor property)
		{
			Type itemType;
			ModelContext.Current.GetModelType<T>().TryGetListItemType(property.PropertyType, out itemType);
			return ModelContext.Current.GetModelType(itemType ?? property.PropertyType);
		}

		protected override TypeConverter GetValueConverter(PropertyDescriptor property)
		{
			return property.Converter;
		}

		protected override Type GetValueType(PropertyDescriptor property)
		{
			return property.PropertyType;
		}

		protected override object GetPropertyValue(object instance, PropertyDescriptor property)
		{
			return property.GetValue(instance);
		}

		protected override void SetPropertyValue(object instance, PropertyDescriptor property, object value)
		{
			property.SetValue(instance, value);
		}

		#endregion
	}
}
