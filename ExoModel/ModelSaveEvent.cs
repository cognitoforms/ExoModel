using System;
using System.Collections.Generic;

namespace ExoModel
{
	/// <summary>
	/// Represents the creation of a new or existing model instance.
	/// </summary>
	public class ModelSaveEvent : ModelEvent, ITransactedModelEvent
	{
		public ModelSaveEvent(ModelInstance instance)
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

		public IEnumerable<ModelInstance> Added { get; internal set; }

		public IEnumerable<ModelInstance> Modified { get; internal set; }

		public IEnumerable<ModelInstance> Deleted { get; internal set; }

		#region ITransactedModelEvent Members

		public void Perform(ModelTransaction transaction)
		{
			Instance = EnsureInstance(transaction, Instance);
			Instance.Save();
		}

		public void Rollback(ModelTransaction transaction)
		{
			throw new NotSupportedException("Rollback is not supported by the ModelSaveEvent.");
		}

		#endregion
	}
}
