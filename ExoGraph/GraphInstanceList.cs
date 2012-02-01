using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace ExoGraph
{
	/// <summary>
	/// Exposes an editable list of instances for a specific list property.
	/// </summary>
	public class GraphInstanceList : ICollection<GraphInstance>, IFormattable
	{
		#region Fields

		GraphInstance owner;
		GraphReferenceProperty property;

		#endregion

		#region Constructors

		internal GraphInstanceList(GraphInstance owner, GraphReferenceProperty property)
		{
			this.owner = owner;
			this.property = property;
		}

		#endregion

		#region Methods

		internal IList GetList()
		{
			return property.DeclaringType.ConvertToList(property, property.GetValue(owner == null ? null : owner.Instance));
		}

		/// <summary>
		/// Gets the string representation of the current list, with each item formatted using the
		/// specified format, separated by commas.
		/// </summary>
		/// <param name="format"></param>
		/// <returns></returns>
		public string ToString(string format)
		{
			return ((IFormattable)this).ToString(format, null);
		}

		/// <summary>
		/// Gets the string representation of the current list, with each item formatted using the
		/// default property format, separated by commas.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return ToString(property.Format);
		}

		#endregion

		#region IFormattable

		/// <summary>
		/// Gets the string representation of the current list, with each item formatted using the
		/// specified format, separated by commas.
		/// </summary>
		/// <param name="format"></param>
		/// <param name="formatProvider"></param>
		/// <returns></returns>
		string  IFormattable.ToString(string format, IFormatProvider formatProvider)
		{
			return this.Aggregate("", (result, i) => result + "," + i);
		}

		#endregion

		#region ICollection<GraphInstance> Members

		/// <summary>
		/// Adds the specified instance to the list.
		/// </summary>
		/// <param name="item"></param>
		public void Add(GraphInstance item)
		{
			IList list = GetList();
			if (list == null)
				throw new NullReferenceException("Cannot add item '" + item + "' to the " + property.Name + " list on '" + owner + "' because the list is null.");
			list.Add(item.Instance);
		}

		/// <summary>
		/// Removes all of the instances from the list.
		/// </summary>
		public void Clear()
		{
			// Get the list and exit immediately if it does not contain any items
			IList list = GetList();
			if (list == null || list.Count == 0)
				return;

			// Remove all of the items from the list
			GraphEventScope.Perform(() =>
			{
				list.Clear();
			});
		}

		/// <summary>
		/// Determines if the specified instance is in the list.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Contains(GraphInstance item)
		{
			IList list = GetList();
			return list != null && list.Contains(item.Instance);
		}

		/// <summary>
		/// Copies the instances into an array.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		void ICollection<GraphInstance>.CopyTo(GraphInstance[] array, int arrayIndex)
		{
			// Get the list and exit immediately if there are no items to copy
			IList list = GetList();
			if (list == null || list.Count == 0)
				return;

			// Copy all instances in the list to the specified array
			foreach (object instance in list)
				array[arrayIndex++] = property.DeclaringType.GetGraphInstance(instance);
		}

		/// <summary>
		/// Gets the number of items in the list.
		/// </summary>
		public int Count
		{
			get
			{
				IList list = GetList();
				return list == null ? 0 : list.Count;
			}
		}

		/// <summary>
		/// Indicates whether the list of read only.
		/// </summary>
		bool ICollection<GraphInstance>.IsReadOnly
		{
			get
			{
				IList list = GetList();
				return list == null || list.IsReadOnly;
			}
		}

		/// <summary>
		/// Removes the specified instance from the list.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Remove(GraphInstance item)
		{
			IList list = GetList();
			if (list == null || !list.Contains(item.Instance))
				return false;
			
			list.Remove(item.Instance);
			return true;
		}

		#endregion

		#region IEnumerable<GraphInstance> Members

		IEnumerator<GraphInstance> IEnumerable<GraphInstance>.GetEnumerator()
		{
			IList list = GetList();
			if (list != null)
			{
				foreach (object instance in list)
					yield return property.DeclaringType.GetGraphInstance(instance);
			}
		}

		#endregion

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			IList list = GetList();
			if (list != null)
			{
				foreach (object instance in list)
					yield return property.DeclaringType.GetGraphInstance(instance);
			}
		}

		#endregion
	}
}
