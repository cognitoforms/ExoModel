using System;
using System.Collections.Generic;

namespace ExoGraph
{
	/// <summary>
	/// Represents the creation of a new or existing graph instance.
	/// </summary>
	public class GraphSaveEvent : GraphEvent, ITransactedGraphEvent
	{
		public GraphSaveEvent(GraphInstance instance)
			: base(instance)
		{ }

		protected override void OnNotify()
		{
			Instance.Type.RaiseSave(this);
		}

		public override string ToString()
		{
			return "Saved " + Instance;
		}

		public IEnumerable<GraphInstance> Added { get; internal set; }

		public IEnumerable<GraphInstance> Modified { get; internal set; }

		public IEnumerable<GraphInstance> Deleted { get; internal set; }

		#region ITransactedGraphEvent Members

		public void Perform(GraphTransaction transaction)
		{
			Instance = EnsureInstance(transaction, Instance);
			Instance.Save();
		}

		public void Rollback(GraphTransaction transaction)
		{
			throw new NotSupportedException("Rollback is not supported by the GraphSaveEvent.");
		}

		#endregion
	}
}
