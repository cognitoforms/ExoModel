namespace ExoGraph
{
	/// <summary>
	/// Represents the creation of a new or existing graph instance.
	/// </summary>
	public abstract class GraphInitEvent : GraphEvent
	{
		internal GraphInitEvent(GraphInstance instance)
			: base(instance)
		{ }

		protected override void OnNotify()
		{
			for (GraphType type = Instance.Type; type != null; type = type.BaseType)
				type.RaiseInit(this);
		}

		public override string ToString()
		{
			return "Initialized " + Instance;
		}

		#region InitNew

		/// <summary>
		/// Represents the creation of a new <see cref="GraphInstance"/>.
		/// </summary>
		public class InitNew : GraphInitEvent, ITransactedGraphEvent
		{
			public InitNew(GraphInstance instance)
				: base(instance)
			{ }

			#region ITransactedGraphEvent Members

			/// <summary>
			/// Creates a new <see cref="GraphInstance"/> of the specified <see cref="GraphType"/>.
			/// </summary>
			void ITransactedGraphEvent.Perform(GraphTransaction transaction)
			{
				// Creates a new instance
				if (Instance.Instance == null)
				{
					// Get the id of the instance surrogate
					string id = Instance.Id;

					// Create a new instance and assign this to the instance the event is for
					Instance = Instance.Type.Create();

					// Set the id of the new instance to the id of the original surrogate
					Instance.Id = id;
					Instance = Instance;
					
					// Force the new instance to initialize
					Instance.OnAccess();
				}
			}

			/// <summary>
			/// Deletes and removes the reference to the <see cref="GraphInstance"/> associated with
			/// the current event, which effectively removes the instance from existence.
			/// </summary>
			void ITransactedGraphEvent.Rollback(GraphTransaction transaction)
			{
				// Ensure that the current instance has been resolved
				Instance = EnsureInstance(transaction, Instance);

				if (Instance.Instance != null)
				{
					// Delete the current instance
					Instance.IsPendingDelete = true;

					// Create a new proxy reference to the instance
					Instance = new GraphInstance(Instance.Type, Instance.Id);
				}
			}

			#endregion
		}

		#endregion

		#region InitExisting

		/// <summary>
		/// Represents the creation of an existing <see cref="GraphInstance"/>.
		/// </summary>
		public class InitExisting : GraphInitEvent
		{
			public InitExisting(GraphInstance instance)
				: base(instance)
			{ }
		}

		#endregion
	}
}
