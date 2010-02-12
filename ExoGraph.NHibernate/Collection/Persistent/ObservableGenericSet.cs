using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Collection.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using NHibernate.Engine;
using Iesi.Collections.Generic;
using NHibernate.Persister.Collection;

namespace ExoGraph.NHibernate.Collection.Persistent
{
	public class ObservableGenericSet<T> : PersistentGenericSet<T>, INotifyPropertyChanged, INotifyCollectionChanged, IEditableObject
	{
		private NotifyCollectionChangedEventHandler collectionChanged;
		private PropertyChangedEventHandler propertyChanged;

		public ObservableGenericSet(ISessionImplementor sessionImplementor)
			: base(sessionImplementor)
		{
		}

		public ObservableGenericSet(ISessionImplementor sessionImplementor, HashedSet<T> coll)
			: base(sessionImplementor, coll)
		{
			CaptureEventHandlers(coll);
		}

		public ObservableGenericSet()
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
			CaptureEventHandlers(set);
		}

		private void CaptureEventHandlers(object coll)
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

		public void CancelEdit()
		{
			throw new NotImplementedException();
		}

		public void BeginEdit()
		{
			throw new NotImplementedException();
		}

		public void EndEdit()
		{
			throw new NotImplementedException();
		}
	}
}
