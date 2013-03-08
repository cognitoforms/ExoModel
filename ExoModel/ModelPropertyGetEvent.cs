using System;
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
			// Determine first access after lock has been acquired to ensure
			// that no more than one event will be treated as first access.
			IsFirstAccess = !Instance.HasBeenAccessed(Property);

			// Perform special processing if this is the first time the property has been accessed
			if (IsFirstAccess)
			{
				if (Instance.IsCached)
				{
					bool locked = false;
					try
					{
						Instance.EnterLock(out locked);

						// Abort if property get notifications have been suspended
						if (Instance.IsPropertyBeingAccessed(Property))
							return;

						Instance.SetIsPropertyBeingAccessed(Property, true);
						try
						{
							IsFirstAccess = !Instance.HasBeenAccessed(Property);

							if (IsFirstAccess)
							{
								// Notify the instance that it is being accessed
								Instance.OnAccess();

								// Raise property get notifications
								RaisePropertyGet();

								// Perform special initialization if this is the first time the property has been accessed
								Instance.OnFirstAccess(Property);
							}
							else   // dont bother checking PropertyGetHasSubscriptions
							{
								// Raise property get notifications
								RaisePropertyGet();
							}
						}
						finally
						{
							Instance.SetIsPropertyBeingAccessed(Property, false);
						}
					}
					finally
					{
						Instance.ExitLock(locked);
					}
				}
				else
				{
					// Abort if property get notifications have been suspended
					if (Instance.IsPropertyBeingAccessed(Property))
						return;

					Instance.SetIsPropertyBeingAccessed(Property, true);
					try
					{
						// Notify the instance that it is being accessed
						Instance.OnAccess();

						// Raise property get notifications
						RaisePropertyGet();

						// Perform special initialization if this is the first time the property has been accessed
						Instance.OnFirstAccess(Property);
					}
					finally
					{
						Instance.SetIsPropertyBeingAccessed(Property, false);
					}
				}
			}

			// Otherwise, just raise property get notifications
			else if (Instance.IsCached)
			{
				if (PropertyGetHasSubscriptions)
				{
					bool locked = false;
					try
					{
						Instance.EnterLock(out locked);
						if (Instance.IsPropertyBeingAccessed(Property))
							return;

						Instance.SetIsPropertyBeingAccessed(Property, true);
						try
						{
							RaisePropertyGet();
						}
						finally
						{
							Instance.SetIsPropertyBeingAccessed(Property, false);
						}
					}
					finally
					{
						Instance.ExitLock(locked);
					}
				}
			}
			else
			{
				if (Instance.IsPropertyBeingAccessed(Property))
					return;

				Instance.SetIsPropertyBeingAccessed(Property, true);
				try
				{
					RaisePropertyGet();
				}
				finally
				{
					Instance.SetIsPropertyBeingAccessed(Property, false);
				}
			}
		}

		bool PropertyGetHasSubscriptions
		{
			get
			{
				// Raise property get on all types in the inheritance hierarchy
				for (ModelType type = Instance.Type; type != null; type = type.BaseType)
				{
					if (type.PropertyGetHasSubscriptions)
						return true;

					// Stop walking the type hierarchy if this is the type that declares the property that was accessed
					if (type == Property.DeclaringType)
						return false;
				}

				return false;
			}
		}

		/// <summary>
		/// Raises the <see cref="ModelPropertyGetEvent"/> on all types in the type hierarchy
		/// of the current instance that have the property that is being accessed.
		/// </summary>
		void RaisePropertyGet()
		{
			// Called here so IsFirstAccess is properly set before notifying
			Instance.Type.Context.Notify(this);

			// Raise property get on all types in the inheritance hierarchy
			for (ModelType type = Instance.Type; type != null; type = type.BaseType)
			{
				type.RaisePropertyGet(this);

				// Stop walking the type hierarchy if this is the type that declares the property that was accessed
				if (type == Property.DeclaringType)
					break;
			}
		}

		internal override void Notify()
		{
			ModelEventScope.Perform(this, OnNotify);
		}

		public override string ToString()
		{
			return "Retrieved Property " + Instance.Type.Name + "." + Property.Name + " on " + Instance;
		}
	}
}
