namespace ExoModel
{
	/// <summary>
	/// Represents the creation of a new or existing model instance.
	/// </summary>
	public abstract class ModelInitEvent : ModelEvent
	{
		internal ModelInitEvent(ModelInstance instance)
			: base(instance)
		{ }

		protected override void OnNotify()
		{
			for (ModelType type = Instance.Type; type != null; type = type.BaseType)
				type.RaiseInit(this);
		}

		public override string ToString()
		{
			return "Initialized " + Instance;
		}

		#region InitNew

		/// <summary>
		/// Represents the creation of a new <see cref="ModelInstance"/>.
		/// </summary>
		public class InitNew : ModelInitEvent, ITransactedModelEvent
		{
			public InitNew(ModelInstance instance)
				: base(instance)
			{ }

			#region ITransactedModelEvent Members

			/// <summary>
			/// Creates a new <see cref="ModelInstance"/> of the specified <see cref="ModelType"/>.
			/// </summary>
			void ITransactedModelEvent.Perform(ModelTransaction transaction)
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
			/// Deletes and removes the reference to the <see cref="ModelInstance"/> associated with
			/// the current event, which effectively removes the instance from existence.
			/// </summary>
			void ITransactedModelEvent.Rollback(ModelTransaction transaction)
			{
				// Ensure that the current instance has been resolved
				Instance = EnsureInstance(transaction, Instance);

				if (Instance.Instance != null)
				{
					// Delete the current instance
					Instance.IsPendingDelete = true;

					// Create a new proxy reference to the instance
					Instance = new ModelInstance(Instance.Type, Instance.Id);
				}
			}

			#endregion
		}

		#endregion

		#region InitExisting

		/// <summary>
		/// Represents the creation of an existing <see cref="ModelInstance"/>.
		/// </summary>
		public class InitExisting : ModelInitEvent
		{
			public InitExisting(ModelInstance instance)
				: base(instance)
			{ }
		}

		#endregion
	}
}
