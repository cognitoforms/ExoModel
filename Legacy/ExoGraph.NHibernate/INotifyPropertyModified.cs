using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoGraph.NHibernate
{
	public interface INotifyPropertyModified
	{
		event PropertyModifiedEventHandler PropertyModified;
	}

	public delegate void PropertyModifiedEventHandler(object sender, PropertyModifiedEventArgs e);

	public class PropertyModifiedEventArgs : EventArgs
	{
		public virtual string PropertyName { get; private set; }
		public virtual object OldValue { get; private set; }
		public virtual object NewValue { get; private set; }

		public PropertyModifiedEventArgs(string propertyName, object oldValue, object newValue)
		{
			PropertyName = propertyName;
			OldValue = oldValue;
			NewValue = newValue;
		}
	}
}
