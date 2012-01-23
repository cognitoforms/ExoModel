namespace ExoGraph
{
	/// <summary>
	/// Represents a change to an reference property in the graph.
	/// </summary>
	public class GraphReferenceChangeEvent : GraphEvent, ITransactedGraphEvent
	{
		public GraphReferenceChangeEvent(GraphInstance instance, GraphReferenceProperty property, GraphInstance oldValue, GraphInstance newValue)
			: base(instance)
		{
			this.Property = property;
			this.OldValue = oldValue;
			this.OldValueId = (oldValue != null) ? oldValue.Id : null;
			this.NewValue = newValue;
			this.NewValueId = (newValue != null) ? newValue.Id : null;
		}

		public GraphReferenceProperty Property { get; private set; }
		
		public GraphInstance OldValue { get; private set; }

		/// <summary>
		/// Gets the id of the old value at the moment the event occurred, which may be different than the current id of the old value.
		/// </summary>
		public string OldValueId { get; private set; }

		public GraphInstance NewValue { get; private set; }

		/// <summary>
		/// Gets the id of the new value at the moment the event occurred, which may be different than the current id of the new value.
		/// </summary>
		public string NewValueId { get; private set; }

		/// <summary>
		/// Indicates whether the current event is valid and represents a real change to the model.
		/// </summary>
		internal override bool IsValid
		{
			get
			{
				return (OldValue == null ^ NewValue == null) || (OldValue != null && !OldValue.Equals(NewValue));
			}
		}

		protected override void OnNotify()
		{
			if (OldValue != null)
				Instance.RemoveReference(Instance.GetOutReference(Property, OldValue));

			if (NewValue != null)
				Instance.AddReference(Property, NewValue, false);

			// Raise reference change on all types in the inheritance hierarchy
			for (GraphType type = Instance.Type; type != null; type = type.BaseType)
			{
				type.RaiseReferenceChange(this);

				// Stop walking the type hierarchy if this is the type that declares the property that was accessed
				if (type == Property.DeclaringType)
					break;
			}
		}

		/// <summary>
		/// Merges a <see cref="GraphValueChangeEvent"/> into the current event.
		/// </summary>
		/// <param name="e"></param>
		/// <returns></returns>
		protected override bool OnMerge(GraphEvent e)
		{
			// Ensure the events are for the same reference property
			if (((GraphReferenceChangeEvent)e).Property != Property)
				return false;

			NewValue = ((GraphReferenceChangeEvent)e).NewValue;
			NewValueId = ((GraphReferenceChangeEvent)e).NewValueId;
			return true;
		}

		public override string ToString()
		{
			return string.Format("Changed {0} on '{1}' from '{2}' to '{3}'", Property, Instance, OldValue, NewValue);
		}

		#region ITransactedGraphEvent Members

		/// <summary>
		/// Sets the reference property to the new value.
		/// </summary>
		void ITransactedGraphEvent.Perform(GraphTransaction transaction)
		{
			Instance = EnsureInstance(transaction, Instance);

			if (OldValue != null)
				OldValue = EnsureInstance(transaction, OldValue);

			if (NewValue != null)
				NewValue = EnsureInstance(transaction, NewValue);

			Property.SetValue(Instance.Instance, NewValue == null ? null : NewValue.Instance);
		}

		/// <summary>
		/// Sets the reference property back to the old value.
		/// </summary>
		void ITransactedGraphEvent.Rollback(GraphTransaction transaction)
		{
			Instance = EnsureInstance(transaction, Instance);

			if (OldValue != null)
				OldValue = EnsureInstance(transaction, OldValue);

			if (NewValue != null)
				NewValue = EnsureInstance(transaction, NewValue);

			Property.SetValue(Instance.Instance, OldValue == null ? null : OldValue.Instance);
		}

		#endregion
	}
}
