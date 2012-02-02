using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	public interface IModelPropertySource
	{
		/// <summary>
		/// Gets the set of properties exposed by the current <see cref="IModelPropertySource"/>.
		/// </summary>
		ModelPropertyList Properties
		{
			get;
		}

		/// <summary>
		/// Gets or sets the value of the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The underlying value of the property in the physical model</returns>
		object this[string property]
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the value of the specified property.
		/// </summary>
		/// <param name="property">The <see cref="ModelProperty"/> to get or set</param>
		/// <returns>The underlying value of the property in the physical model</returns>
		object this[ModelProperty property]
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		ModelInstance GetReference(string property);

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelReferenceProperty"/></param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		ModelInstance GetReference(ModelReferenceProperty property);

		/// <summary>
		/// Gets the value assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The value of the property</returns>
		object GetValue(string property);

		/// <summary>
		/// Gets the value assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelValueProperty"/></param>
		/// <returns>The value of the property</returns>
		object GetValue(ModelValueProperty property);

		/// <summary>
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <param name="format">The optional format to use</param>
		/// <returns>The formatted value of the property</returns>
		string GetFormattedValue(string property, string format);

		/// <summary>
		/// Gets the formatted value of the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelProperty"/></param>
		/// <param name="format">The optional format to use</param>
		/// <returns>The formatted value of the property</returns>
		string GetFormattedValue(ModelProperty property, string format);

		/// <summary>
		/// Gets the list of <see cref="ModelInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of property</param>
		/// <returns>The list of instances</returns>
		ModelInstanceList GetList(string property);

		/// <summary>
		/// Gets the list of <see cref="ModelInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="ModelReferenceProperty"/></param>
		/// <returns>The list of instances</returns>
		ModelInstanceList GetList(ModelReferenceProperty property);

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		void SetReference(string property, ModelInstance value);

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		void SetReference(ModelReferenceProperty property, ModelInstance value);

		/// <summary>
		/// Sets a property to the specified value.
		/// </summary>
		/// <param name="property">The property to set</param>
		/// <param name="value">The value of the property</param>
		void SetValue(string property, object value);

		/// <summary>
		/// Sets a property to the specified value.
		/// </summary>
		/// <param name="property">The property to set</param>
		/// <param name="value">The value of the property</param>
		void SetValue(ModelValueProperty property, object value);
	}
}
