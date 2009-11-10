using System;

namespace ExoGraph
{
	public class GraphFilterChangedEventArgs : EventArgs
	{
		GraphFilter filter;

		internal GraphFilterChangedEventArgs(GraphFilter filter)
		{
			this.filter = filter;
		}

		public GraphFilter Filter
		{
			get
			{
				return filter;
			}
		}
	}
}
