using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Type;
using NHibernate.UserTypes;
using ExoGraph.NHibernate.Collection.PersistentImpl;
using System.Collections;
using System.Collections.ObjectModel;
using NHibernate.Collection;
using NHibernate.Engine;
using NHibernate.Persister.Collection;

namespace ExoGraph.NHibernate.Collection.Types
{
	public class ObservableBagType<T> : CollectionType, IUserCollectionType
	{
		public ObservableBagType(string role, string foreignKeyPropertyName, bool isEmbeddedInXML)
			: base(role, foreignKeyPropertyName, isEmbeddedInXML)
		{
		}

		public ObservableBagType()
			: base(string.Empty, string.Empty, false)
		{

		}
		public IPersistentCollection Instantiate(ISessionImplementor session, ICollectionPersister persister)
		{
			return new PersistentObservableGenericBag<T>(session);
		}

		public override IPersistentCollection Instantiate(ISessionImplementor session, ICollectionPersister persister, object key)
		{
			return new PersistentObservableGenericBag<T>(session);
		}

		public override IPersistentCollection Wrap(ISessionImplementor session, object collection)
		{
			return new PersistentObservableGenericBag<T>(session, (ICollection<T>) collection);
		}

		public IEnumerable GetElements(object collection)
		{
			return ((IEnumerable) collection);
		}

		public bool Contains(object collection, object entity)
		{
			return ((ICollection<T>) collection).Contains((T) entity);
		}


		public object ReplaceElements(object original, object target, ICollectionPersister persister, object owner, IDictionary copyCache, ISessionImplementor session)
		{
			return base.ReplaceElements(original, target, owner, copyCache, session);
		}

		public override object Instantiate(int anticipatedSize)
		{
			return new FullyObservableCollection<T>();
		}

		public override Type ReturnedClass
		{
			get
			{
				return typeof(PersistentObservableGenericBag<T>);
			}
		}
	}
}
