using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoGraph
{
	public interface IGraphPropertySource
	{
		/// <summary>
		/// Gets the set of properties exposed by the current <see cref="IGraphPropertySource"/>.
		/// </summary>
		GraphPropertyList Properties
		{
			get;
		}

		/// <summary>
		/// Gets or sets the value of the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The underlying value of the property in the physical graph</returns>
		object this[string property]
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the value of the specified property.
		/// </summary>
		/// <param name="property">The <see cref="GraphProperty"/> to get or set</param>
		/// <returns>The underlying value of the property in the physical graph</returns>
		object this[GraphProperty property]
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the <see cref="GraphInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		GraphInstance GetReference(string property);

		/// <summary>
		/// Gets the <see cref="GraphInstance"/> assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphReferenceProperty"/></param>
		/// <returns>The instance assigned to the property, or null if the property does not have a value</returns>
		GraphInstance GetReference(GraphReferenceProperty property);

		/// <summary>
		/// Gets the value assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of the property</param>
		/// <returns>The value of the property</returns>
		object GetValue(string property);

		/// <summary>
		/// Gets the value assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphValueProperty"/></param>
		/// <returns>The value of the property</returns>
		object GetValue(GraphValueProperty property);

		/// <summary>
		/// Gets the list of <see cref="GraphInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The name of property</param>
		/// <returns>The list of instances</returns>
		GraphInstanceList GetList(string property);

		/// <summary>
		/// Gets the list of <see cref="GraphInstance"/> items assigned to the specified property.
		/// </summary>
		/// <param name="property">The specific <see cref="GraphReferenceProperty"/></param>
		/// <returns>The list of instances</returns>
		GraphInstanceList GetList(GraphReferenceProperty property);

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		void SetReference(string property, GraphInstance value);

		/// <summary>
		/// Sets the reference for a property to the specified instance.
		/// </summary>
		/// <param name="property">The property the reference is for</param>
		/// <param name="value">The value of the property</param>
		void SetReference(GraphReferenceProperty property, GraphInstance value);

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
		void SetValue(GraphValueProperty property, object value);
	}
}
