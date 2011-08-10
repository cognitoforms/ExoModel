using System.Runtime.Serialization;
namespace ExoGraph
{
	/// <summary>
	/// Represents the retrieval of a property in the graph.
	/// </summary>
	[DataContract]
	public class GraphPropertyGetEvent : GraphEvent
	{
		internal GraphPropertyGetEvent(GraphInstance instance, GraphProperty property)
			: base(instance)
		{
			this.Property = property;
			this.IsFirstAccess = !Instance.HasBeenAccessed(property);
		}

		public GraphProperty Property { get; private set; }

		public bool IsFirstAccess { get; private set; }

		protected override bool OnNotify()
		{
			var context = Instance.Type.Context;

			// Abort if property get notifications have been suspended
			if (context.IsPropertyBeingAccessed(Instance, Property))
				return false;

			try
			{
				// Prevent gets from recursively raising get notifications
				context.AddPendingPropertyGet(Instance, Property);

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
				context.RemovePendingPropertyGet(Instance, Property);
			}

			// Indicate that the notification should be raised by the context
			return true;
		}

		/// <summary>
		/// Raises the <see cref="GraphPropertyGetEvent"/> on all types in the type hierarchy
		/// of the current instance that have the property that is being accessed.
		/// </summary>
		void RaisePropertyGet()
		{
			// Raise property get on all types in the inheritance hierarchy
			for (GraphType type = Instance.Type; type != null; type = type.BaseType)
			{
				type.RaisePropertyGet(this);

				// Stop walking the type hierarchy if this is the type that declares the property that was accessed
				if (type == Property.DeclaringType)
					break;
			}
		}

		public override string ToString()
		{
			return "Retrieved Property " + Instance;
		}
	}
}
