namespace ExoGraph
{
	/// <summary>
	/// Represents the creation of a graph instance.
	/// </summary>
	public class GraphCustomEvent<TEvent> : GraphEvent
	{
		TEvent customEvent;

		internal GraphCustomEvent(GraphInstance instance, TEvent customEvent)
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

		protected override void OnNotify()
		{
			Instance.Type.RaiseEvent(this);
		}

		#region Transacted

		internal class Transacted<TEvent> : GraphCustomEvent<TEvent>, ITransactedGraphEvent
			where TEvent : ITransactedGraphEvent
		{
			internal Transacted(GraphInstance instance, TEvent customEvent)
				: base(instance, customEvent)
			{ }

			#region ITransactedGraphEvent Members

			void ITransactedGraphEvent.Perform()
			{
				CustomEvent.Perform();
			}

			void ITransactedGraphEvent.Commit()
			{
				CustomEvent.Commit();
			}

			void ITransactedGraphEvent.Rollback()
			{
				CustomEvent.Rollback();
			}

			#endregion
		}

		#endregion
	}
}
