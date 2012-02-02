using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Type;
using ExoGraph.NHibernate.Collection.Type;

namespace ExoGraph.NHibernate.Collection
{
	public class ObservableCollectionTypeFactory : DefaultCollectionTypeFactory
	{
		public override CollectionType Bag<T>(string role, string propertyRef, bool embedded)
		{
			return new ObservableBag<T>(role, propertyRef, embedded);
		}

		public override CollectionType Set<T>(string role, string propertyRef, bool embedded)
		{
			return new ExoGraph.NHibernate.Collection.Type.ObservableSet<T>(role, propertyRef, embedded);
		}

		public override CollectionType List<T>(string role, string propertyRef, bool embedded)
		{
			return new ObservableList<T>(role, propertyRef, embedded);
		}
	}
}
