using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Collection.Generic;
using System.ComponentModel;
using System.Collections.Specialized;
using NHibernate.Engine;
using NHibernate.Persister.Collection;

namespace ExoGraph.NHibernate.Collection.PersistentImpl
{
	public class PersistentObservableGenericList<T> : PersistentGenericList<T>, INotifyCollectionChanged,
													  INotifyPropertyChanged, IList<T>
	{
		private NotifyCollectionChangedEventHandler collectionChanged;
		private PropertyChangedEventHandler propertyChanged;

		public PersistentObservableGenericList(ISessionImplementor sessionImplementor)
			: base(sessionImplementor)
		{
		}

		public PersistentObservableGenericList(ISessionImplementor sessionImplementor, IList<T> list)
			: base(sessionImplementor, list)
		{
			CaptureEventHandlers(list);
		}

		public PersistentObservableGenericList()
		{
		}

		#region INotifyCollectionChanged Members

		public event NotifyCollectionChangedEventHandler CollectionChanged
		{
			add
			{
				Initialize(false);
				collectionChanged += value;
			}
			remove { collectionChanged -= value; }
		}

		#endregion

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
				Initialize(false);
				propertyChanged += value;
			}
			remove { propertyChanged += value; }
		}

		#endregion

		public override void BeforeInitialize(ICollectionPersister persister, int anticipatedSize)
		{
			base.BeforeInitialize(persister, anticipatedSize);
			CaptureEventHandlers((ICollection<T>) list);
		}

		private void CaptureEventHandlers(ICollection<T> coll)
		{
			var notificableCollection = coll as INotifyCollectionChanged;
			var propertyNotificableColl = coll as INotifyPropertyChanged;

			if (notificableCollection != null)
				notificableCollection.CollectionChanged += OnCollectionChanged;

			if (propertyNotificableColl != null)
				propertyNotificableColl.PropertyChanged += OnPropertyChanged;
		}

		private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChangedEventHandler changed = propertyChanged;
			if (changed != null) changed(this, e);
		}

		private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			NotifyCollectionChangedEventHandler changed = collectionChanged;
			if (changed != null) changed(this, e);
		}
	}
}
