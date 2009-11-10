using System.Collections.Generic;
using System.Collections;

namespace ExoGraph
{
	/// <summary>
	/// Base class for read only lists of items keyed by name.
	/// </summary>
	public abstract class ReadOnlyList<TItem> : IEnumerable<TItem>
	{
		Dictionary<string, TItem> list = new Dictionary<string, TItem>();

		/// <summary>
		/// Gets the number of items in the list.
		/// </summary>
		public int Count
		{
			get
			{
				return list.Count;
			}
		}

		/// <summary>
		/// Gets the item in the list with the specified name or
		/// returns null if an item does not exist with the given name.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public TItem this[string name]
		{
			get
			{
				TItem type;
				list.TryGetValue(name, out type);
				return type;
			}
		}

		/// <summary>
		/// Determines whether an item in the list exists with the specified name.
		/// </summary>
		/// <param name="name">The name of the item to find</param>
		/// <returns>True if the item exists, otherwise false</returns>
		public bool Contains(string name)
		{
			return list.ContainsKey(name);
		}

		/// <summary>
		/// Determines whether an item is in the list.
		/// </summary>
		/// <param name="item">The item to find</param>
		/// <returns>True if the item exists, otherwise false</returns>
		public bool Contains(TItem item)
		{
			return list.ContainsValue(item);
		}

		/// <summary>
		/// Enumerates over the items in the list.
		/// </summary>
		/// <returns></returns>
		IEnumerator<TItem> IEnumerable<TItem>.GetEnumerator()
		{
			return list.Values.GetEnumerator();
		}

		/// <summary>
		/// Enumerates over the items in the list.
		/// </summary>
		/// <returns></returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return list.Values.GetEnumerator();
		}

		/// <summary>
		/// Returns the name of the item.
		/// </summary>
		/// <param name="item"></param>
		protected abstract string GetName(TItem item);

		/// <summary>
		/// Allows subclasses to add items to the internal list.
		/// </summary>
		/// <param name="item">The item to add</param>
		internal void Add(TItem item)
		{
			list.Add(GetName(item), item);
		}
	}
}
