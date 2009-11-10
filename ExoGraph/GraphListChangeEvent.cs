using System.Collections.Generic;

namespace ExoGraph
{
	/// <summary>
	/// Represents the additional or removal of instances from a list associated with a parent graph instance.
	/// </summary>
	public class GraphListChangeEvent : GraphEvent
	{
		GraphReferenceProperty property;
		IEnumerable<GraphInstance> added;
		IEnumerable<GraphInstance> removed;

		internal GraphListChangeEvent(GraphInstance instance, GraphReferenceProperty property, IEnumerable<GraphInstance> added, IEnumerable<GraphInstance> removed)
			: base(instance)
		{
			this.property = property;
			this.added = added;
			this.removed = removed;
		}

		public GraphReferenceProperty Property
		{
			get
			{
				return property;
			}
		}

		public IEnumerable<GraphInstance> Added
		{
			get
			{
				return added;
			}
		}

		public IEnumerable<GraphInstance> Removed
		{
			get
			{
				return removed;
			}
		}

		/// <summary>
		/// Reverts the instance to its original state before the event was performed.
		/// </summary>
		public override void Revert()
		{
			using (new GraphEventScope())
			{
				GraphContext context = Instance.Type.Context;
				if (added != null)
				{
					foreach (GraphInstance item in added)
						context.RemoveFromList(Instance.Instance, Property.Name, item.Instance);
				}
				if (removed != null)
				{
					foreach (GraphInstance item in removed)
						context.AddToList(Instance.Instance, Property.Name, item.Instance);
				}
			}
		}

		protected override void OnNotify()
		{
			foreach (GraphInstance ri in removed)
				Instance.RemoveReference(Instance.GetOutReference(Property, ri));

			foreach (GraphInstance ai in added)
				Instance.AddReference(property, ai, false);

			Instance.Type.RaiseListChange(this);
		}
	}
}
