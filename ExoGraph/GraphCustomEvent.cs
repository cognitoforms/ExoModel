namespace ExoGraph
{
	/// <summary>
	/// Represents the creation of a graph instance.
	/// </summary>
	public class GraphCustomEvent<TEvent> : GraphEvent
	{
		TEvent customEvent;

		public GraphCustomEvent(GraphInstance instance, TEvent customEvent)
			: base(instance)
		{
			this.customEvent = customEvent;
		}

		public TEvent CustomEvent
		{
			get
			{
				return customEvent;
			}
		}

		protected override bool OnNotify()
		{
			Instance.Type.RaiseEvent(this);

			// Indicate that the notification should be raised by the context
			return true;
		}

		#region Transacted

		internal class Transacted<TEvent> : GraphCustomEvent<TEvent>, ITransactedGraphEvent
			where TEvent : ITransactedGraphEvent
		{
			internal Transacted(GraphInstance instance, TEvent customEvent)
				: base(instance, customEvent)
			{ }

			#region ITransactedGraphEvent Members

			void ITransactedGraphEvent.Perform(GraphTransaction transaction)
			{
				CustomEvent.Perform(transaction);
			}

			void ITransactedGraphEvent.Commit(GraphTransaction transaction)
			{
				CustomEvent.Commit(transaction);
			}

			void ITransactedGraphEvent.Rollback(GraphTransaction transaction)
			{
				CustomEvent.Rollback(transaction);
			}

			#endregion
		}

		#endregion
	}
}
