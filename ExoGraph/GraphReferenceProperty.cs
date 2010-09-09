using System;
namespace ExoGraph
{
	/// <summary>
	/// Represents a property that associates two types in a graph hierarchy.
	/// </summary>
	[Serializable]
	public abstract class GraphReferenceProperty : GraphProperty
	{
		#region Constructors

		protected internal GraphReferenceProperty(GraphType declaringType, string name, bool isStatic, bool isBoundary, GraphType propertyType, bool isList, Attribute[] attributes)
			: base(declaringType, name, isStatic, isList, attributes)
		{
			this.PropertyType = propertyType;
			this.IsBoundary = isBoundary;
		}

		#endregion

		#region Properties

		public GraphType PropertyType { get; private set; }

		public bool IsBoundary { get; private set; }

		#endregion
	}
}
