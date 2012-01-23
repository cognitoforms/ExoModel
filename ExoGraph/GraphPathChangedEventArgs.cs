using System;

namespace ExoGraph
{
	/// <summary>
	/// Represents a change the a property along a path in the model.
	/// </summary>
	public class GraphPathChangeEvent : GraphEvent
	{
		GraphPath path;

		internal GraphPathChangeEvent(GraphInstance instance, GraphPath path)
			: base(instance)
		{
			this.path = path;
		}

		public GraphPath Path
		{
			get
			{
				return path;
			}
		}

		protected override void OnNotify()
		{
			throw new NotSupportedException("Path change events do not broadcast globally.");
		}
	}
}
