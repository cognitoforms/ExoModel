using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoGraph.NHibernate
{
	public interface INotifyPropertyAccessed
	{
		event PropertyAccessedEventHandler PropertyAccessed;
	}

	public delegate void PropertyAccessedEventHandler(object sender, PropertyAccessedEventArgs e);

	public class PropertyAccessedEventArgs : EventArgs
	{
		public virtual string PropertyName { get; private set; }

		public PropertyAccessedEventArgs(string propertyName)
		{
			PropertyName = propertyName;
		}
	}
}
