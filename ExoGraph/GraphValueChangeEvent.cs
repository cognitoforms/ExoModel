namespace ExoGraph
{
	/// <summary>
	/// Represents a change to a value property in the graph.
	/// </summary>
	public class GraphValueChangeEvent : GraphEvent
	{
		GraphValueProperty property;
		object originalValue;
		object currentValue;

		internal GraphValueChangeEvent(GraphInstance instance, GraphValueProperty property, object originalValue, object currentValue)
			: base(instance)
		{
			this.property = property;
			this.originalValue = originalValue;
			this.currentValue = currentValue;
		}

		public GraphValueProperty Property
		{
			get
			{
				return property;
			}
		}

		public object OriginalValue
		{
			get
			{
				return originalValue;
			}
		}

		public object CurrentValue
		{
			get
			{
				return currentValue;
			}
		}

		/// <summary>
		/// Notify subscribers that the property value has changed.
		/// </summary>
		protected override void OnNotify()
		{
			property.OnChange(Instance);

			Instance.Type.RaiseValueChange(this);
		}

		/// <summary>
		/// Restores the property to the original value.
		/// </summary>
		public override void Revert()
		{
			Instance.Type.Context.SetProperty(Instance.Instance, Property.Name, OriginalValue);
		}

		/// <summary>
		/// Returns the description of the property value change.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return string.Format("Changed {0} on '{1}' from '{2}' to '{3}'", Property, Instance, OriginalValue, CurrentValue);
		}
	}
}
