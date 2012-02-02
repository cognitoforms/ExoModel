namespace ExoModel
{
	/// <summary>
	/// Represents a change in the status of an instance being pending deletion.
	/// </summary>
	public class ModelDeleteEvent : ModelEvent, ITransactedModelEvent
	{
		public ModelDeleteEvent(ModelInstance instance, bool isPendingDelete)
			: base(instance)
		{
			this.IsPendingDelete = isPendingDelete;
		}

		public bool IsPendingDelete { get; private set; }

		protected override void OnNotify()
		{
			for (ModelType type = Instance.Type; type != null; type = type.BaseType)
				type.RaiseDelete(this);
		}

		public override string ToString()
		{
			return "Deleted " + Instance;
		}

		/// <summary>
		/// Changes the pending deletion status of the instance to the specified value.
		/// </summary>
		/// <param name="transaction"></param>
		void ITransactedModelEvent.Perform(ModelTransaction transaction)
		{
			// Ensure that the current instance has been resolved
			Instance = EnsureInstance(transaction, Instance);

			// Change the pending deletion status of the instance to the specified value
			Instance.IsPendingDelete = IsPendingDelete;
		}

		/// <summary>
		/// Restores the pending deletion status of the instance to the original value.
		/// </summary>
		/// <param name="transaction"></param>
		void ITransactedModelEvent.Rollback(ModelTransaction transaction)
		{
			// Ensure that the current instance has been resolved
			Instance = EnsureInstance(transaction, Instance);

			// Restore the pending deletion status of the instance to the original value
			Instance.IsPendingDelete = !IsPendingDelete;
		}
	}
}
