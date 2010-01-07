using System;

namespace ExoGraph
{
	/// <summary>
	/// Represents a property that exposes strongly-typed data as leaves of a graph hierarchy.
	/// </summary>
	public abstract class GraphValueProperty : GraphProperty
	{
		#region Constructors

		internal GraphValueProperty(GraphType declaringType, string name, bool isStatic, Type propertyType, Attribute[] attributes)
			: base(declaringType, name, isStatic, attributes)
		{
			this.PropertyType = propertyType;
		}

		#endregion

		#region Properties

		public Type PropertyType { get; private set; }

		#endregion
	}
}
