using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ExoGraph.NHibernate.Collection
{
	public class FullyObservableCollection<T> : ObservableCollection<T>
	{
		protected override void ClearItems()
		{
			for (int i = this.Count - 1; i >= 0; i--)
				this.RemoveAt(i);
		}
	}
}
