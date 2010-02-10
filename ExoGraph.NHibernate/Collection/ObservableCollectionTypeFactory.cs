using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Type;
using ExoGraph.NHibernate.Collection.Types;

namespace ExoGraph.NHibernate.Collection
{
	public class ObservableCollectionTypeFactory : DefaultCollectionTypeFactory
	{
		public override CollectionType Bag<T>(string role, string propertyRef, bool embedded)
		{
			return new ObservableBagType<T>(role, propertyRef, embedded);
		}

		public override CollectionType Set<T>(string role, string propertyRef, bool embedded)
		{
			return new ObservableSetType<T>(role, propertyRef, embedded);
		}

		public override CollectionType List<T>(string role, string propertyRef, bool embedded)
		{
			return new ObservableListType<T>(role, propertyRef, embedded);
		}
	}
}
