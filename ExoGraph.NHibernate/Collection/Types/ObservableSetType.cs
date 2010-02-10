using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Type;
using NHibernate.UserTypes;
using ExoGraph.NHibernate.Collection.PersistentImpl;
using System.Collections;
using NHibernate.Engine;
using NHibernate.Persister.Collection;
using Iesi.Collections.Generic;
using NHibernate.Collection;

namespace ExoGraph.NHibernate.Collection.Types
{
	public class ObservableSetType<T> : CollectionType, IUserCollectionType
	{
		public ObservableSetType(string role, string foreignKeyPropertyName, bool isEmbeddedInXML)
			: base(role, foreignKeyPropertyName, isEmbeddedInXML)
		{
		}

		public ObservableSetType()
			: base(string.Empty, string.Empty, false)
		{
		}

		public override Type ReturnedClass
		{
			get { return typeof(PersistentObservableGenericSet<T>); }
		}

		#region IUserCollectionType Members

		public IPersistentCollection Instantiate(ISessionImplementor session, ICollectionPersister persister)
		{
			return new PersistentObservableGenericSet<T>(session);
		}

		public override IPersistentCollection Wrap(ISessionImplementor session, object collection)
		{
			return new PersistentObservableGenericSet<T>(session, (ISet<T>) collection);
		}

		public IEnumerable GetElements(object collection)
		{
			return ((IEnumerable) collection);
		}

		public bool Contains(object collection, object entity)
		{
			return ((ICollection<T>) collection).Contains((T) entity);
		}


		public object ReplaceElements(object original, object target, ICollectionPersister persister, object owner,
									  IDictionary copyCache, ISessionImplementor session)
		{
			return base.ReplaceElements(original, target, owner, copyCache, session);
		}

		public override object Instantiate(int anticipatedSize)
		{
			return new ObservableSet<T>();
		}

		#endregion

		public override IPersistentCollection Instantiate(ISessionImplementor session, ICollectionPersister persister,
														  object key)
		{
			return new PersistentObservableGenericSet<T>(session);
		}
	}
}
