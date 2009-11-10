namespace ExoGraph
{
	/// <summary>
	/// Represents the creation of a graph instance.
	/// </summary>
	public class GraphInitEvent : GraphEvent
	{
		internal GraphInitEvent(GraphInstance instance)
			: base(instance)
		{ }

		protected override void OnNotify()
		{
			Instance.Type.RaiseInit(this);
		}

		public override void Revert()
		{
			Instance.Delete();
		}

		public override string ToString()
		{
			return "Initialized " + Instance;
		}
	}
}
