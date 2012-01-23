using System.Linq;
using System.Collections.Generic;

namespace ExoGraph
{
	/// <summary>
	/// Represents the additional or removal of instances from a list associated with a parent graph instance.
	/// </summary>
	public class GraphListChangeEvent : GraphEvent, ITransactedGraphEvent
	{
		public GraphListChangeEvent(GraphInstance instance, GraphReferenceProperty property, IEnumerable<GraphInstance> added, IEnumerable<GraphInstance> removed)
			: base(instance)
		{
			this.Property = property;
			this.Added = added.ToArray();
			this.AddedIds = this.Added.Select(i => i.Id).ToArray();
			this.Removed = removed.ToArray();
			this.RemovedIds = this.Removed.Select(i => i.Id).ToArray();
		}

		public GraphReferenceProperty Property { get; private set; }

		public IEnumerable<GraphInstance> Added { get; private set; }

		public IEnumerable<string> AddedIds { get; private set; }

		public IEnumerable<GraphInstance> Removed { get; private set; }

		public IEnumerable<string> RemovedIds { get; private set; }

		/// <summary>
		/// Indicates whether the current event is valid and represents a real change to the model.
		/// </summary>
		internal override bool IsValid
		{
			get
			{
				return Added.Any() || Removed.Any();
			}
		}

		protected override void OnNotify()
		{
			foreach (GraphInstance ri in Removed)
				Instance.RemoveReference(Instance.GetOutReference(Property, ri));

			foreach (GraphInstance ai in Added)
				Instance.AddReference(Property, ai, false);

			// Raise reference change on all types in the inheritance hierarchy
			for (GraphType type = Instance.Type; type != null; type = type.BaseType)
			{
				type.RaiseListChange(this);

				// Stop walking the type hierarchy if this is the type that declares the property that was accessed
				if (type == Property.DeclaringType)
					break;
			}
		}

		/// <summary>
		/// Merges a <see cref="GraphValueChangeEvent"/> into the current event.
		/// </summary>
		/// <param name="e"></param>
		/// <returns></returns>
		protected override bool OnMerge(GraphEvent e)
		{
			// Ensure the events are for the same reference property
			var listChange = (GraphListChangeEvent) e;
			if (listChange.Property != Property)
				return false;

			// Highly likely not to be right
			var mergeAdded = Added.ToList();
			mergeAdded.RemoveAll(i => listChange.Removed.Contains(i));

			var mergeRemoved = Removed.ToList();
			mergeRemoved.RemoveAll(i => listChange.Added.Contains(i));

			mergeAdded = mergeAdded.Union(listChange.Added.Where(i => !Removed.Contains(i))).ToList();
			mergeRemoved = mergeRemoved.Union(listChange.Removed.Where(i => !Added.Contains(i))).ToList();

			Added = mergeAdded.ToArray();
			Removed = mergeRemoved.ToArray();


			var mergeAddedIds = AddedIds.ToList();
			mergeAddedIds.RemoveAll(i => listChange.RemovedIds.Contains(i));

			var mergeRemovedIds = RemovedIds.ToList();
			mergeRemovedIds.RemoveAll(i => listChange.AddedIds.Contains(i));

			mergeAddedIds = mergeAddedIds.Union(listChange.AddedIds.Where(i => !RemovedIds.Contains(i))).ToList();
			mergeRemovedIds = mergeRemovedIds.Union(listChange.RemovedIds.Where(i => !AddedIds.Contains(i))).ToList();

			AddedIds = mergeAddedIds.ToArray();
			RemovedIds = mergeRemovedIds.ToArray();

			return true;
		}

		public override string ToString()
		{
			return string.Format("Added {0} items to and removed {1} items from '{2}'", Added.Count(), Removed.Count(), Property.Name);
		}

		#region ITransactedGraphEvent Members

		void Prepare(GraphTransaction transaction)
		{
			// Resolve the root instance
			Instance = EnsureInstance(transaction, Instance);

			// Resolve added instances
			var added = (GraphInstance[])Added;
			for (int i = 0; i < added.Length; i++)
				added[i] = EnsureInstance(transaction, added[i]);

			// Resolve removed instances
			var removed = (GraphInstance[])Removed;
			for (int i = 0; i < removed.Length; i++)
				removed[i] = EnsureInstance(transaction, removed[i]);
		}

		void ITransactedGraphEvent.Perform(GraphTransaction transaction)
		{
			Prepare(transaction);

			GraphEventScope.Perform(() =>
			{
				GraphContext context = Instance.Type.Context;
				GraphInstanceList list = Instance.GetList(Property);

				if (Added != null)
				{
					foreach (GraphInstance item in Added)
						list.Add(item);
				}
				if (Removed != null)
				{
					foreach (GraphInstance item in Removed)
						list.Remove(item);
				}
			});
		}

		void ITransactedGraphEvent.Rollback(GraphTransaction transaction)
		{
			Prepare(transaction);

			GraphEventScope.Perform(() =>
			{
				GraphContext context = Instance.Type.Context;
				GraphInstanceList list = Instance.GetList(Property);

				if (Added != null)
				{
					foreach (GraphInstance item in Added)
						list.Remove(item);
				}
				if (Removed != null)
				{
					foreach (GraphInstance item in Removed)
						list.Add(item);
				}
			});
		}

		#endregion
	}
}
