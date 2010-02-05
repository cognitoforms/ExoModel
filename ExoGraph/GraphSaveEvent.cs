using System.Runtime.Serialization;
using System.Collections.Generic;
namespace ExoGraph
{
	/// <summary>
	/// Represents the creation of a new or existing graph instance.
	/// </summary>
	[DataContract(Name = "Save")]
	public class GraphSaveEvent : GraphEvent, ITransactedGraphEvent
	{
		List<IdChange> idChanges;

		internal GraphSaveEvent(GraphInstance instance)
			: base(instance)
		{ }

		protected override bool OnNotify()
		{
			Instance.Type.RaiseSave(this);

			// Indicate that the notification should be raised by the context
			return true;
		}

		public override string ToString()
		{
			return "Saved " + Instance;
		}

		internal void AddIdChange(GraphType type, string oldId, string newId)
		{
			if (idChanges == null)
				idChanges = new List<IdChange>();
			idChanges.Add(new IdChange(type, oldId, newId));
		}

		[DataMember(Name = "idChanges", Order = 2)]
		public IEnumerable<IdChange> IdChanges
		{
			get
			{
				return idChanges;
			}
		}

		#region ITransactedGraphEvent Members

		public void Perform(GraphTransaction transaction)
		{
			Instance.Save();
		}

		public void Commit(GraphTransaction transaction)
		{ }

		public void Rollback(GraphTransaction transaction)
		{ }

		#endregion

		#region IdChange

		[DataContract]
		public class IdChange
		{
			internal IdChange(GraphType type, string oldId, string newId)
			{
				this.Type = type;
				this.OldId = oldId;
				this.NewId = newId;
			}

			public GraphType Type { get; private set; }

			[DataMember(Name = "type", Order = 1)]
			string TypeName
			{
				get
				{
					return Type.Name;
				}
				set
				{
					Type = GraphContext.Current.GetGraphType(value);
				}
			}

			[DataMember(Name = "oldId", Order = 2)]
			public string OldId { get; private set; }

			[DataMember(Name = "newId", Order = 3)]
			public string NewId { get; private set; }
		}

		#endregion
	}
}
