namespace ExoModel
{
	/// <summary>
	/// Represents the retrieval of a property in the model.
	/// </summary>
	public class ModelPropertyGetEvent : ModelEvent
	{
		internal ModelPropertyGetEvent(ModelInstance instance, ModelProperty property)
			: base(instance)
		{
			this.Property = property;
		}

		public ModelProperty Property { get; private set; }

		public bool IsFirstAccess { get; private set; }

		protected override void OnNotify()
		{
			// Lock cached objects before notifying to prevent multi-threaded rule execution
			using(Instance.IsCached ? Instance.Lock() : null)
			{
				// Determine first access after lock has been acquired to ensure
				// that no more than one event will be treated as first access.
				this.IsFirstAccess = !Instance.HasBeenAccessed(Property);
	
				var context = Instance.Type.Context;

				// Abort if property get notifications have been suspended
				if (Instance.IsPropertyBeingAccessed(Property))
					return;

				try
				{
					// Prevent gets from recursively raising get notifications
					Instance.SetIsPropertyBeingAccessed(Property, true);

					// Perform special processing if this is the first time the property has been accessed
					if (IsFirstAccess)
					{
						// Notify the instance that it is being accessed
						Instance.OnAccess();

						// Raise property get notifications
						RaisePropertyGet();

						// Perform special initialization if this is the first time the property has been accessed
						Instance.OnFirstAccess(Property);
					}

					// Otherwise, just raise property get notifications
					else
						RaisePropertyGet();
				}
				finally
				{
					Instance.SetIsPropertyBeingAccessed(Property, false);
				}
			}
		}

		/// <summary>
		/// Raises the <see cref="ModelPropertyGetEvent"/> on all types in the type hierarchy
		/// of the current instance that have the property that is being accessed.
		/// </summary>
		void RaisePropertyGet()
		{
			// Raise property get on all types in the inheritance hierarchy
			for (ModelType type = Instance.Type; type != null; type = type.BaseType)
			{
				type.RaisePropertyGet(this);

				// Stop walking the type hierarchy if this is the type that declares the property that was accessed
				if (type == Property.DeclaringType)
					break;
			}
		}

		public override string ToString()
		{
			return "Retrieved Property " + Instance.Type.Name + "." + Property.Name + " on " + Instance;
		}
	}
}
