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
		GraphInstance originalValue;
		GraphInstance currentValue;

		internal GraphReferenceChangeEvent(GraphInstance instance, GraphReferenceProperty property, GraphInstance originalValue, GraphInstance currentValue)
			: base(instance)
		{
			this.property = property;
			this.originalValue = originalValue;
			this.currentValue = currentValue;
		}

		public GraphReferenceProperty Property
		{
			get
			{
				return property;
			}
		}

		[DataMember(Name = "Property", Order = 2)]
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

		[DataMember(Order = 3)]
		public GraphInstance OriginalValue
		{
			get
			{
				return originalValue;
			}
			private set
			{
				this.originalValue = value;
			}
		}

		[DataMember(Order = 4)]
		public GraphInstance CurrentValue
		{
			get
			{
				return currentValue;
			}
			private set
			{
				this.originalValue = value;
			}
		}

		protected override void OnNotify()
		{
			if (OriginalValue != null)
				Instance.RemoveReference(Instance.GetOutReference(property, OriginalValue));

			if (CurrentValue != null)
				Instance.AddReference(property, CurrentValue, false);

			Instance.Type.RaiseReferenceChange(this);
		}

		public override string ToString()
		{
			return string.Format("Changed {0} on '{1}' from '{2}' to '{3}'", Property, Instance, OriginalValue, CurrentValue);
		}

		#region ITransactedGraphEvent Members

		/// <summary>
		/// Sets the reference property to the current value.
		/// </summary>
		void ITransactedGraphEvent.Perform()
		{
			Instance.Type.Context.SetProperty(Instance.Instance, Property.Name, CurrentValue == null ? null : CurrentValue.Instance);
		}

		void ITransactedGraphEvent.Commit()
		{ }

		/// <summary>
		/// Sets the reference property back to the original value.
		/// </summary>
		void ITransactedGraphEvent.Rollback()
		{
			Instance.Type.Context.SetProperty(Instance.Instance, Property.Name, OriginalValue == null ? null : OriginalValue.Instance);
		}

		#endregion
	}
}
