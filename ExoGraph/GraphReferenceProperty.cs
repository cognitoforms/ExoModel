using System;
namespace ExoGraph
{
	/// <summary>
	/// Represents a property that associates two types in a graph hierarchy.
	/// </summary>
	[Serializable]
	public class GraphReferenceProperty : GraphProperty
	{
		#region Fields

		GraphType propertyType;
		bool isList;
		bool isBoundary;

		#endregion

		#region Constructors

		internal GraphReferenceProperty(GraphType declaringType, string name, int index, bool isStatic, bool isBoundary, GraphType propertyType, bool isList, Attribute[] attributes)
			: base(declaringType, name, index, isStatic, attributes)
		{
			this.propertyType = propertyType;
			this.isList = isList;
			this.isBoundary = isBoundary;
		}

		#endregion

		#region Properties

		public GraphType PropertyType
		{
			get
			{
				return propertyType;
			}
		}

		public bool IsList
		{
			get
			{
				return isList;
			}
		}

		public bool IsBoundary
		{
			get
			{
				return isBoundary;
			}
		}

		#endregion
	}
}
