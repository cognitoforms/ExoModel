using System.Runtime.Serialization;
namespace ExoGraph
{
	/// <summary>
	/// Represents the retrieval of a property in the graph.
	/// </summary>
	[DataContract]
	public class GraphPropertyGetEvent : GraphEvent
	{
		internal GraphPropertyGetEvent(GraphInstance instance, GraphProperty property)
			: base(instance)
		{
			this.Property = property;
			this.IsFirstAccess = !Instance.HasBeenAccessed(property);
		}

		public GraphProperty Property { get; private set; }

		public bool IsFirstAccess { get; private set; }

		protected override bool OnNotify()
		{
			// Perform special processing if this is the first time the property has been accessed
			if (IsFirstAccess)
			{
				// Perform special initialization if this is the first time the property has been accessed
				Instance.BeforeFirstAccess(Property);

				// Abort if property get notifications have been suspended
				if (Instance.Type.Context.ShouldSuspendGetNotifications)
					return false;

				// Notify the instance that it is being accessed
				Instance.OnAccess();

				// Raise property get notifications
				Instance.Type.RaisePropertyGet(this);

				// Perform special initialization if this is the first time the property has been accessed
				Instance.OnFirstAccess(Property);
			}

			// Otherwise, just raise property get notifications
			else
				Instance.Type.RaisePropertyGet(this);

			// Indicate that the notification should be raised by the context
			return true;
		}

		public override string ToString()
		{
			return "Retrieved Property " + Instance;
		}
	}
}
