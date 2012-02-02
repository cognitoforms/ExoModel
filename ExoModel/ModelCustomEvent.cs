namespace ExoModel
{
	/// <summary>
	/// Represents the raising of a custom domain event.
	/// </summary>
	public class ModelCustomEvent<TEvent> : ModelEvent
	{
		TEvent customEvent;

		/// <summary>
		/// Creates a new <see cref="ModelCustomEvent"/> for the specified instance
		/// and concrete event object payload.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="customEvent"></param>
		public ModelCustomEvent(ModelInstance instance, TEvent customEvent)
			: base(instance)
		{
			this.customEvent = customEvent;
		}

		/// <summary>
		/// The payload for the event being raised.
		/// </summary>
		public TEvent CustomEvent
		{
			get
			{
				return customEvent;
			}
		}

		/// <summary>
		/// Notifies subscribers that this custom event has been raised.
		/// </summary>
		/// <returns></returns>
		protected override void OnNotify()
		{
			Instance.Type.RaiseEvent(this);
		}
	}
}
