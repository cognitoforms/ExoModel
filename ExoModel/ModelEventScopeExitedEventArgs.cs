using System;

namespace ExoModel
{
	public class ModelEventScopeExitedEventArgs : EventArgs
	{
		ModelEventScope scope;

		public ModelEventScopeExitedEventArgs(ModelEventScope scope)
		{
			this.scope = scope;
		}

		public ModelEventScope Scope
		{
			get
			{
				return scope;
			}
		}
	}
}
