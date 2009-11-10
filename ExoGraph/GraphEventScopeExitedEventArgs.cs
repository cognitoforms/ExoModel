using System;

namespace ExoGraph
{
	public class GraphEventScopeExitedEventArgs : EventArgs
	{
		GraphEventScope scope;

		public GraphEventScopeExitedEventArgs(GraphEventScope scope)
		{
			this.scope = scope;
		}

		public GraphEventScope Scope
		{
			get
			{
				return scope;
			}
		}
	}
}
