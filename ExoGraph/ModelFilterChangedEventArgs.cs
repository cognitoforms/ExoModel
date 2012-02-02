using System;

namespace ExoModel
{
	public class ModelFilterChangedEventArgs : EventArgs
	{
		ModelFilter filter;

		internal ModelFilterChangedEventArgs(ModelFilter filter)
		{
			this.filter = filter;
		}

		public ModelFilter Filter
		{
			get
			{
				return filter;
			}
		}
	}
}
