using System.Runtime.Serialization;
namespace ExoGraph
{
	/// <summary>
	/// Represents the creation of a graph instance.
	/// </summary>
	[DataContract(Name = "Delete")]
	public class GraphDeleteEvent : GraphEvent
	{
		public GraphDeleteEvent(GraphInstance instance)
			: base(instance)
		{ }

		protected override bool OnNotify()
		{
			// Indicate that the notification should be raised by the context
			return true;
		}

		public override string ToString()
		{
			return "Deleted " + Instance;
		}
	}
}
