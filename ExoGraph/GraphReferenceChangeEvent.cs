namespace ExoGraph
{
	/// <summary>
	/// Represents a change to an reference property in the graph.
	/// </summary>
	public class GraphReferenceChangeEvent : GraphEvent
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

		public GraphInstance OriginalValue
		{
			get
			{
				return originalValue;
			}
		}

		public GraphInstance CurrentValue
		{
			get
			{
				return currentValue;
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

		public override void Revert()
		{
			Instance.Type.Context.SetProperty(Instance.Instance, Property.Name, OriginalValue == null ? null : OriginalValue.Instance);
		}

		public override string ToString()
		{
			return string.Format("Changed {0} on '{1}' from '{2}' to '{3}'", Property, Instance, OriginalValue, CurrentValue);
		}
	}
}
