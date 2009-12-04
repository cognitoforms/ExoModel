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

		[DataMember(Name = "Property")]
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

		[DataMember(Name = "Added")]
		GraphInstance[] AddedArray
		{
			get
			{
				return added.ToArray<GraphInstance>();
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


		[DataMember(Name = "Removed")]
		GraphInstance[] RemovedArray
		{
			get
			{
				return removed.ToArray<GraphInstance>();
			}
			set
			{
				removed = value;
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

		#region ITransactedGraphEvent Members

		void ITransactedGraphEvent.Perform()
		{
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

		void ITransactedGraphEvent.Commit()
		{ }

		void ITransactedGraphEvent.Rollback()
		{
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
