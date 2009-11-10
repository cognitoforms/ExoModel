using System;

namespace ExoGraph
{
	/// <summary>
	/// Represents a property that exposes strongly-typed data as leaves of a graph hierarchy.
	/// </summary>
	public class GraphValueProperty : GraphProperty
	{
		#region Fields

		Type propertyType;

		#endregion

		#region Constructors

		internal GraphValueProperty(GraphType declaringType, string name, int index, Type propertyType)
			: base(declaringType, name, index)
		{
			this.propertyType = propertyType;
		}

		#endregion

		#region Properties

		public Type PropertyType
		{
			get
			{
				return propertyType;
			}
		}

		#endregion
	}
}
