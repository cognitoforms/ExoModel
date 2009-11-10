namespace ExoGraph
{
	/// <summary>
	/// Represents the creation of a graph instance.
	/// </summary>
	public class GraphCustomEvent<TEvent> : GraphEvent
	{
		TEvent customEvent;

		internal GraphCustomEvent(GraphInstance instance, TEvent customEvent)
			: base(instance)
		{
			this.customEvent = customEvent;
		}

		public TEvent CustomEvent
		{
			get
			{
				return customEvent;
			}
		}

		public override void Revert()
		{
			// TODO: Figure out a pattern to allow custom events to support optional reversion
		}

		protected override void OnNotify()
		{
			Instance.Type.RaiseEvent(this);
		}
	}
}
