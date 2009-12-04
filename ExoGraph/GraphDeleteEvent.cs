using System.Runtime.Serialization;
namespace ExoGraph
{
	/// <summary>
	/// Represents the creation of a graph instance.
	/// </summary>
	[DataContract(Name = "Delete")]
	public class GraphDeleteEvent : GraphEvent
	{
		internal GraphDeleteEvent(GraphInstance instance)
			: base(instance)
		{ }

		protected override void OnNotify()
		{
			//Instance.Type.RaiseDelete(this);
		}

		public override string ToString()
		{
			return "Deleted " + Instance;
		}
	}
}
