namespace ExoGraph
{
	/// <summary>
	/// Represents the retrieval of a property in the graph.
	/// </summary>
	public class GraphPropertyGetEvent : GraphEvent
	{
		GraphProperty property;

		internal GraphPropertyGetEvent(GraphInstance instance, GraphProperty property)
			: base(instance)
		{
			this.property = property;
		}

		public bool IsFirstAccess
		{
			get
			{
				return !Instance.HasBeenAccessed(property);
			}
		}

		public GraphProperty Property
		{
			get
			{
				return property;
			}
		}

		protected override void OnNotify()
		{
			// Notify the instance that it is being accessed
			Instance.OnAccess();

			// Raise property get notifications
			Instance.Type.RaisePropertyGet(this);

			// Perform special initialization if this is the first time the property has been accessed
			if (IsFirstAccess)
				Instance.OnFirstAccess(property);
		}

		public override void Revert()
		{ }

		public override string ToString()
		{
			return "Retrieved Property " + Instance;
		}
	}
}
