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
		string[] addedIds;
		GraphInstance[] removed;
		string[] removedIds;

		public GraphListChangeEvent(GraphInstance instance, GraphReferenceProperty property, IEnumerable<GraphInstance> added, IEnumerable<GraphInstance> removed)
			: base(instance)
		{
			this.property = property;
			this.AddedArray = added.ToArray();
			this.RemovedArray = removed.ToArray();
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
				addedIds = value.Select(i => i.Id).ToArray();
			}
		}

		public IEnumerable<string> AddedIds
		{
			get
			{
				return addedIds;
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
				removedIds = value.Select(i => i.Id).ToArray();
			}
		}

		public IEnumerable<string> RemovedIds
		{
			get
			{
				return removedIds;
			}
		}

		/// <summary>
		/// Indicates whether the current event is valid and represents a real change to the model.
		/// </summary>
		internal override bool IsValid
		{
			get
			{
				return added.Length > 0 || removed.Length > 0;
			}
		}

		protected override void OnNotify()
		{
			foreach (GraphInstance ri in removed)
				Instance.RemoveReference(Instance.GetOutReference(Property, ri));

			foreach (GraphInstance ai in added)
				Instance.AddReference(property, ai, false);

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
			var mergeAdded = added.ToList();
			mergeAdded.RemoveAll(i => listChange.removed.Contains(i));

			var mergeRemoved = removed.ToList();
			mergeRemoved.RemoveAll(i => listChange.added.Contains(i));

			mergeAdded = mergeAdded.Union(listChange.added.Where(i => !removed.Contains(i))).ToList();
			mergeRemoved = mergeRemoved.Union(listChange.removed.Where(i => !added.Contains(i))).ToList();

			added = mergeAdded.ToArray();
			removed = mergeRemoved.ToArray();


			var mergeAddedIds = addedIds.ToList();
			mergeAddedIds.RemoveAll(i => listChange.removedIds.Contains(i));

			var mergeRemovedIds = removedIds.ToList();
			mergeRemovedIds.RemoveAll(i => listChange.addedIds.Contains(i));

			mergeAddedIds = mergeAddedIds.Union(listChange.addedIds.Where(i => !removedIds.Contains(i))).ToList();
			mergeRemovedIds = mergeRemovedIds.Union(listChange.removedIds.Where(i => !addedIds.Contains(i))).ToList();

			addedIds = mergeAddedIds.ToArray();
			removedIds = mergeRemovedIds.ToArray();

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

			GraphEventScope.Perform(() =>
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
			});
		}

		void ITransactedGraphEvent.Commit(GraphTransaction transaction)
		{ }

		void ITransactedGraphEvent.Rollback(GraphTransaction transaction)
		{
			Prepare(transaction);

			GraphEventScope.Perform(() =>
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
			});
		}

		#endregion
	}
}
