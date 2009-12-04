using System.Runtime.Serialization;
namespace ExoGraph
{
	/// <summary>
	/// Represents a change to a value property in the graph.
	/// </summary>
	[DataContract(Name = "ValueChange")]
	public class GraphValueChangeEvent : GraphEvent, ITransactedGraphEvent
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

		[DataMember]
		public object OriginalValue
		{
			get
			{
				return originalValue;
			}
			private set
			{
				originalValue = value;
			}
		}

		[DataMember]
		public object CurrentValue
		{
			get
			{
				return currentValue;
			}
			private set
			{
				currentValue = value;
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
		/// Returns the description of the property value change.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return string.Format("Changed {0} on '{1}' from '{2}' to '{3}'", Property, Instance, OriginalValue, CurrentValue);
		}

		#region ITransactedGraphEvent Members

		void ITransactedGraphEvent.Perform()
		{
			throw new System.NotImplementedException();
		}

		void ITransactedGraphEvent.Commit()
		{
			throw new System.NotImplementedException();
		}

		/// <summary>
		/// Restores the property to the original value.
		/// </summary>
		void ITransactedGraphEvent.Rollback()
		{
			Instance.Type.Context.SetProperty(Instance.Instance, Property.Name, OriginalValue);
		}

		#endregion
	}
}
