using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Data.Objects.DataClasses;
using System.Data.Objects;
using System.Collections;
using System.Collections.Specialized;

namespace ExoModel.EntityFramework
{
	public class CollectionWrapper<T> : IList, INotifyCollectionChanged
		where T : class
	{
		private class CollectionChangedPassthrough
		{
			NotifyCollectionChangedEventHandler	handler;

			internal CollectionChangedPassthrough(NotifyCollectionChangedEventHandler handler)
			{
				this.handler = handler;
			}

			internal void OnCollectionChanged(object sender, CollectionChangeEventArgs e)
			{
				NotifyCollectionChangedAction action = NotifyCollectionChangedAction.Add;
				switch (e.Action)
				{
					case CollectionChangeAction.Add:
						action = NotifyCollectionChangedAction.Add;
						break;
					case CollectionChangeAction.Refresh:
						action = NotifyCollectionChangedAction.Reset;
						break;
					case CollectionChangeAction.Remove:
						action = NotifyCollectionChangedAction.Remove;
						break;
				}

				NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(action, e.Element);

				handler(sender, args);
			}

			public override bool  Equals(object obj)
			{
				return obj is CollectionChangedPassthrough && (obj as CollectionChangedPassthrough).handler.Equals(handler);
			}

			public override int GetHashCode()
			{
				return handler.GetHashCode();
			}
		}

		EntityCollection<T> collection;

		#region INotifyCollectionChanged Members

		public event NotifyCollectionChangedEventHandler CollectionChanged 
		{
		    add
		    {
		        collection.AssociationChanged += new CollectionChangedPassthrough(value).OnCollectionChanged;
		    }
		    remove
		    {
				collection.AssociationChanged -= new CollectionChangedPassthrough(value).OnCollectionChanged;
			}
		}

		#endregion

		public CollectionWrapper(EntityCollection<T> collection)
		{
			this.collection = collection;			
		}

		#region IList Members

		public int Add(object value)
		{
			collection.Add((T) value);

			return collection.Count - 1;
		}

		public void Clear()
		{
			collection.Clear();
		}

		public bool Contains(object value)
		{
			return collection.Contains((T) value);
		}

		public int IndexOf(object value)
		{
			return (collection as IListSource).GetList().IndexOf(value);
		}

		public void Insert(int index, object value)
		{
			throw new NotImplementedException();
			//collection.Insert(index, (T) value);

			//OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
		}

		public bool IsFixedSize
		{
			get 
			{
				return (collection as IListSource).GetList().IsFixedSize;
			}
		}

		public bool IsReadOnly
		{
			get 
			{
				return collection.IsReadOnly;
			}
		}

		public void Remove(object value)
		{
			collection.Remove((T) value);
		}

		public void RemoveAt(int index)
		{
			T oldItem = collection.ElementAt(index);

			if (oldItem != null)
				collection.Remove(oldItem);
		}

		public object this[int index]
		{
			get
			{
				return collection.ElementAt(index);
			}
			set
			{
				this.Insert(index, value);
			}
		}

		#endregion

		#region ICollection Members

		public void CopyTo(Array array, int index)
		{
			throw new NotImplementedException();
			//collection.CopyTo(array, index);
		}

		public int Count
		{
			get 
			{
				return collection.Count;
			}
		}

		public bool IsSynchronized
		{
			get 
			{
				return (collection as IListSource).GetList().IsSynchronized;
			}
		}

		public object SyncRoot
		{
			get 
			{
				return (collection as IListSource).GetList().SyncRoot;
			}
		}

		#endregion

		#region IEnumerable Members

		public IEnumerator GetEnumerator()
		{
			return collection.GetEnumerator();
		}

		#endregion
	}
}
