﻿using System.Runtime.Serialization;
using System;
using System.ServiceModel.Dispatcher;
namespace ExoGraph
{
	/// <summary>
	/// Represents a change to a value property in the graph.
	/// </summary>
	[DataContract(Name = "ValueChange")]
	public class GraphValueChangeEvent : GraphEvent, ITransactedGraphEvent
	{
		// Cache a converter to serialize and deserialize JSON data
		static JsonQueryStringConverter jsonConverter = new JsonQueryStringConverter();
	
		GraphValueProperty property;
		object oldValue;
		object newValue;

		public GraphValueChangeEvent(GraphInstance instance, GraphValueProperty property, object oldValue, object newValue)
			: base(instance)
		{
			this.property = property;

			if (property.AutoConvert)
			{
				this.oldValue = oldValue == null ? null : property.Converter.ConvertTo(oldValue, typeof(object));
				this.newValue = newValue == null ? null : property.Converter.ConvertTo(newValue, typeof(object));
			}
			else
			{
				this.oldValue = oldValue;
				this.newValue = newValue;
			}
		}

		public GraphValueProperty Property
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
				property = (GraphValueProperty)Instance.Type.Properties[value];
			}
		}

		[DataMember(Name = "oldValue", Order = 3)]
		public object OldValue
		{
			get
			{
				return oldValue;
			}
			private set
			{
				if (Property.PropertyType.IsAssignableFrom(typeof(DateTime)) && value is string)
				{
					// Must perform custom date deserialization here since this property is not strongly typed.
					string serializedDate = ((string)value).Replace("/Date(", "\"\\/Date(").Replace(")/", ")\\/\"");
					oldValue = jsonConverter.ConvertStringToValue(serializedDate, typeof(DateTime));
				}
				else
					oldValue = value;
			}
		}


		[DataMember(Name = "newValue", Order = 4)]
		public object NewValue
		{
			get
			{
				return newValue;
			}
			private set
			{
				if (Property.PropertyType.IsAssignableFrom(typeof(DateTime)) && value is string)
				{
					// Must perform custom date deserialization here since this property is not strongly typed.
					string serializedDate = ((string)value).Replace("/Date(", "\"\\/Date(").Replace(")/", ")\\/\"");
					newValue = jsonConverter.ConvertStringToValue(serializedDate, typeof(DateTime));
				}
				else
					newValue = value;
			}
		}

		/// <summary>
		/// Notify subscribers that the property value has changed.
		/// </summary>
		protected override bool OnNotify()
		{
			property.OnChange(Instance);

			Instance.Type.RaiseValueChange(this);

			// Indicate that the notification should be raised by the context
			return true;
		}

		/// <summary>
		/// Returns the description of the property value change.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return string.Format("Changed {0} on '{1}' from '{2}' to '{3}'", Property, Instance, OldValue, NewValue);
		}

		#region ITransactedGraphEvent Members

		void ITransactedGraphEvent.Perform(GraphTransaction transaction)
		{
			Instance = EnsureInstance(transaction, Instance);
			Instance.SetValue(Property, NewValue);
		}

		void ITransactedGraphEvent.Commit(GraphTransaction transaction)
		{ }

		/// <summary>
		/// Restores the property to the old value.
		/// </summary>
		void ITransactedGraphEvent.Rollback(GraphTransaction transaction)
		{
			Instance = EnsureInstance(transaction, Instance);
			Instance.SetValue(Property, OldValue);
		}

		#endregion
	}
}
