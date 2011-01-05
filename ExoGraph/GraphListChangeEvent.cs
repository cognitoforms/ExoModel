using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ExoGraph
{
	/// <summary>
	/// Represents the additional or removal of instances from a list associated with a parent graph instance.
	/// </summary>
	[DataContract(Name = "ListChange")]
	public class GraphListChangeEvent : GraphEvent, ITransactedGraphEvent
	{
		GraphReferenceProperty property;
		GraphInstance[] added;
		GraphInstance[] removed;

		public GraphListChangeEvent(GraphInstance instance, GraphReferenceProperty property, IEnumerable<GraphInstance> added, IEnumerable<GraphInstance> removed)
			: base(instance)
		{
			this.property = property;
			this.added = added.ToArray();
			this.removed = removed.ToArray();
		}

		public GraphReferenceProperty Property
		{
			get
			{
				return property;
			}
		}

		[DataMember(Name = "property", Order = 2)]
		string PropertyName
		{
			get
			{
				return property.Name;
			}
			set
			{
				property = Instance.Type.OutReferences[value];
			}
		}

		public IEnumerable<GraphInstance> Added
		{
			get
			{
				return added;
			}
		}

		[DataMember(Name = "added", Order = 3)]
		GraphInstance[] AddedArray
		{
			get
			{
				return added;
			}
			set
			{
				added = value;
			}
		}

		public IEnumerable<GraphInstance> Removed
		{
			get
			{
				return removed;
			}
		}


		[DataMember(Name = "removed", Order = 4)]
		GraphInstance[] RemovedArray
		{
			get
			{
				return removed;
			}
			set
			{
				removed = value;
			}
		}

		protected override bool OnNotify()
		{
			foreach (GraphInstance ri in removed)
				Instance.RemoveReference(Instance.GetOutReference(Property, ri));

			foreach (GraphInstance ai in added)
				Instance.AddReference(property, ai, false);

			Instance.Type.RaiseListChange(this);

			// Indicate that the notification should be raised by the context
			return true;
		}

		public override string ToString()
		{
			return string.Format("Added {0} items to and removed {1} items from '{2}'", AddedArray.Length, RemovedArray.Length, PropertyName);
		}

		#region ITransactedGraphEvent Members

		void Prepare(GraphTransaction transaction)
		{
			// Resolve the root instance
			Instance = EnsureInstance(transaction, Instance);

			// Resolve added instances
			for (int i = 0; i < added.Length; i++)
				added[i] = EnsureInstance(transaction, added[i]);

			// Resolve removed instances
			for (int i = 0; i < removed.Length; i++)
				removed[i] = EnsureInstance(transaction, removed[i]);
		}

		void ITransactedGraphEvent.Perform(GraphTransaction transaction)
		{
			Prepare(transaction);

			using (new GraphEventScope())
			{
				GraphContext context = Instance.Type.Context;
				GraphInstanceList list = Instance.GetList(Property);

				if (added != null)
				{
					foreach (GraphInstance item in added)
						list.Add(item);
				}
				if (removed != null)
				{
					foreach (GraphInstance item in removed)
						list.Remove(item);
				}
			}
		}

		void ITransactedGraphEvent.Commit(GraphTransaction transaction)
		{ }

		void ITransactedGraphEvent.Rollback(GraphTransaction transaction)
		{
			Prepare(transaction);

			using (new GraphEventScope())
			{
				GraphContext context = Instance.Type.Context;
				GraphInstanceList list = Instance.GetList(Property);

				if (added != null)
				{
					foreach (GraphInstance item in added)
						list.Remove(item);
				}
				if (removed != null)
				{
					foreach (GraphInstance item in removed)
						list.Add(item);
				}
			}
		}

		#endregion
	}
}
