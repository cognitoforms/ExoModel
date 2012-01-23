namespace ExoGraph
{
	/// <summary>
	/// Represents a change in the status of an instance being pending deletion.
	/// </summary>
	public class GraphDeleteEvent : GraphEvent, ITransactedGraphEvent
	{
		public GraphDeleteEvent(GraphInstance instance, bool isPendingDelete)
			: base(instance)
		{
			this.IsPendingDelete = isPendingDelete;
		}

		public bool IsPendingDelete { get; private set; }

		protected override void OnNotify()
		{
			for (GraphType type = Instance.Type; type != null; type = type.BaseType)
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
		void ITransactedGraphEvent.Perform(GraphTransaction transaction)
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
		void ITransactedGraphEvent.Rollback(GraphTransaction transaction)
		{
			// Ensure that the current instance has been resolved
			Instance = EnsureInstance(transaction, Instance);

			// Restore the pending deletion status of the instance to the original value
			Instance.IsPendingDelete = !IsPendingDelete;
		}
	}
}
