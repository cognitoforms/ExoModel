using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections;
using Iesi.Collections.Generic;

namespace ExoGraph.NHibernate.Collection
{
	public class FullyObservableSet<T> : HashedSet<T>, INotifyCollectionChanged, INotifyPropertyChanged
	{
		#region INotifyCollectionChanged Members

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		#endregion

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		private void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			PropertyChangedEventHandler changed = PropertyChanged;
			if (changed != null) changed(this, e);
		}

		private void OnPropertyChanged(string propertyName)
		{
			OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
		}

		private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			NotifyCollectionChangedEventHandler changed = CollectionChanged;
			if (changed != null) changed(this, e);
		}

		private void OnCollectionReset()
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		/// <summary>
		/// Adds the specified element to this set if it is not already present.
		/// </summary>
		/// <param name="o">The <typeparamref name="T"/> to add to the set.</param>
		/// <returns>
		/// <see langword="true"/> is the object was added, <see langword="false"/> if it was already present.
		/// </returns>
		public override bool Add(T element)
		{
			bool result = base.Add(element);
			if (result)
			{
				OnPropertyChanged("IsEmpty");
				OnPropertyChanged("Count");
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, element));
			}

			return result;
		}

		/// <summary>
		/// Removes all objects from the set.
		/// </summary>
		public override void Clear()
		{
			IList<T> oldItems = new List<T>(this.Count);
			foreach (T item in this)
				oldItems.Add(item);

			base.Clear();
			OnPropertyChanged("IsEmpty");
			OnPropertyChanged("Count");
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, null, oldItems));
		}

		/// <summary>
		/// Removes the specified element from the set.
		/// </summary>
		/// <param name="o">The element to be removed.</param>
		/// <returns>
		/// <see langword="true"/> if the set contained the specified element, <see langword="false"/> otherwise.
		/// </returns>
		public override bool Remove(T element)
		{
			var itemIndex = 0;
			foreach (var obj in this)
			{
				if (obj.Equals(element)) break;
				itemIndex++;
			}

			bool result = base.Remove(element);
			if (result)
			{
				OnPropertyChanged("IsEmpty");
				OnPropertyChanged("Count");
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, element, itemIndex));
			}

			return result;
		}

		/// <summary>
		/// Remove all the specified elements from this set, if they exist in this set.
		/// </summary>
		/// <param name="c">A collection of elements to remove.</param>
		/// <returns>
		/// <see langword="true"/> if the set was modified as a result of this operation.
		/// </returns>
		public override bool RemoveAll(ICollection<T> c)
		{
			bool flag = false;

			var itemsRemoved = new List<T>();
			foreach (T item in c)
			{
				bool operationResult = Remove(item);
				flag |= operationResult;
				if (operationResult)
					itemsRemoved.Add(item);
			}
			if (flag)
			{
				OnPropertyChanged("IsEmpty");
				OnPropertyChanged("Count");
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, itemsRemoved));
			}

			return flag;
		}

		/// <summary>
		/// Retains only the elements in this set that are contained in the specified collection.
		/// </summary>
		/// <param name="c">Collection that defines the set of elements to be retained.</param>
		/// <returns>
		/// <see langword="true"/> if this set changed as a result of this operation.
		/// </returns>
		public override bool RetainAll(ICollection<T> c)
		{
			bool flag = false;

			var itemsRemoved = new List<T>();
			foreach (T item in (IEnumerable) Clone())
			{
				if (c.Contains(item))
					continue;

				bool operationResult = Remove(item);
				flag |= operationResult;
				if (operationResult)
					itemsRemoved.Add(item);
			}
			if (flag)
			{
				OnPropertyChanged("IsEmpty");
				OnPropertyChanged("Count");
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, itemsRemoved));
			}

			return flag;
		}

		/// <summary>
		/// Adds all the elements in the specified collection to the set if they are not already present.
		/// </summary>
		/// <param name="c">A collection of objects to add to the set.</param>
		/// <returns>
		/// <see langword="true"/> is the set changed as a result of this operation, <see langword="false"/> if not.
		/// </returns>
		public override bool AddAll(ICollection<T> c)
		{
			bool flag = false;

			var itemsAdded = new List<T>();
			foreach (T item in c)
			{
				bool operationResult = Add(item);
				flag |= operationResult;
				if (operationResult)
					itemsAdded.Add(item);
			}
			if (flag)
			{
				OnPropertyChanged("IsEmpty");
				OnPropertyChanged("Count");
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemsAdded));
			}

			return flag;
		}
	}
}
