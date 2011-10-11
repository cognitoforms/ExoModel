using System.Runtime.Serialization;
namespace ExoGraph
{
	/// <summary>
	/// Represents a change to an reference property in the graph.
	/// </summary>
	[DataContract(Name = "ReferenceChange")]
	public class GraphReferenceChangeEvent : GraphEvent, ITransactedGraphEvent
	{
		GraphReferenceProperty property;
		GraphInstance oldValue;
		GraphInstance newValue;
		string oldValueId;
		string newValueId;

		public GraphReferenceChangeEvent(GraphInstance instance, GraphReferenceProperty property, GraphInstance oldValue, GraphInstance newValue)
			: base(instance)
		{
			this.property = property;
			this.OldValue = oldValue;
			this.NewValue = newValue;
		}

		public GraphReferenceProperty Property
		{
			get
			{
				return property;
			}
		}

		[DataMember(Name = "property", Order = 2)]
		string PropertyName
		{
			get
			{
				return property.Name;
			}
			set
			{
				property = Instance.Type.OutReferences[value];
			}
		}

		[DataMember(Name = "oldValue", Order = 3)]
		public GraphInstance OldValue
		{
			get
			{
				return oldValue;
			}
			private set
			{
				oldValue = value;
				oldValueId = (value != null) ? value.Id : null;
			}
		}

		/// <summary>
		/// Gets the id of the old value at the moment the event occurred, which may be different than the current id of the old value.
		/// </summary>
		public string OldValueId
		{
			get
			{
				return oldValueId;
			}
		}

		[DataMember(Name = "newValue", Order = 4)]
		public GraphInstance NewValue
		{
			get
			{
				return newValue;
			}
			private set
			{
				newValue = value;
				newValueId = (value != null) ? value.Id : null;
			}
		}

		/// <summary>
		/// Gets the id of the new value at the moment the event occurred, which may be different than the current id of the new value.
		/// </summary>
		public string NewValueId
		{
			get
			{
				return newValueId;
			}
		}

		/// <summary>
		/// Indicates whether the current event is valid and represents a real change to the model.
		/// </summary>
		internal override bool IsValid
		{
			get
			{
				return (oldValue == null ^ newValue == null) || (oldValue != null && !oldValue.Equals(newValue));
			}
		}

		protected override bool OnNotify()
		{
			if (OldValue != null)
				Instance.RemoveReference(Instance.GetOutReference(property, OldValue));

			if (NewValue != null)
				Instance.AddReference(property, NewValue, false);

			// Raise reference change on all types in the inheritance hierarchy
			for (GraphType type = Instance.Type; type != null; type = type.BaseType)
			{
				type.RaiseReferenceChange(this);

				// Stop walking the type hierarchy if this is the type that declares the property that was accessed
				if (type == Property.DeclaringType)
					break;
			}

			// Indicate that the notification should be raised by the context
			return true;
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

			newValue = ((GraphReferenceChangeEvent)e).newValue;
			newValueId = ((GraphReferenceChangeEvent)e).newValueId;
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

		void ITransactedGraphEvent.Commit(GraphTransaction transaction)
		{ }

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
