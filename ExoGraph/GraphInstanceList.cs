using System.Collections.Generic;
using System.Collections;

namespace ExoGraph
{
	/// <summary>
	/// Exposes an editable list of instances for a specific list property.
	/// </summary>
	public class GraphInstanceList : ICollection<GraphInstance>
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

		#region ICollection<GraphInstance> Members

		/// <summary>
		/// Adds the specified instance to the list.
		/// </summary>
		/// <param name="item"></param>
		public void Add(GraphInstance item)
		{
			owner.Type.Context.AddToList(owner.Instance, property.Name, item.Instance);
		}

		/// <summary>
		/// Removes all of the instances from the list.
		/// </summary>
		public void Clear()
		{
			using (new GraphEventScope())
			{
				foreach (GraphReference reference in owner.GetOutReferences(property))
					Remove(reference.Out);
			}
		}

		/// <summary>
		/// Determines if the specified instance is in the list.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Contains(GraphInstance item)
		{
			foreach (GraphReference reference in owner.GetOutReferences(property))
				if (reference.Out == item)
					return true;

			return false;
		}

		/// <summary>
		/// Copies the instances into an array.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		void ICollection<GraphInstance>.CopyTo(GraphInstance[] array, int arrayIndex)
		{
			foreach (GraphReference reference in owner.GetOutReferences(property))
				array[arrayIndex++] = reference.Out;
		}

		/// <summary>
		/// Gets the number of items in the list.
		/// </summary>
		public int Count
		{
			get
			{
				int count = 0;
				foreach (GraphReference reference in owner.GetOutReferences(property))
					count++;
				return count;
			}
		}

		/// <summary>
		/// Indicates whether the list of read only.
		/// </summary>
		bool ICollection<GraphInstance>.IsReadOnly
		{
			get
			{
				return false;
			}
		}

		/// <summary>
		/// Removes the specified instance from the list.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Remove(GraphInstance item)
		{
			return owner.Type.Context.RemoveFromList(owner.Instance, property.Name, item.Instance);
		}

		#endregion

		#region IEnumerable<GraphInstance> Members

		IEnumerator<GraphInstance> IEnumerable<GraphInstance>.GetEnumerator()
		{
			foreach (GraphReference reference in owner.GetOutReferences(property))
				yield return reference.Out;
		}

		#endregion

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			foreach (GraphReference reference in owner.GetOutReferences(property))
				yield return reference.Out;
		}

		#endregion
	}
}
