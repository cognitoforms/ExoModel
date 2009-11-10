namespace ExoGraph
{
	/// <summary>
	/// Represents a property that associates two types in a graph hierarchy.
	/// </summary>
	public class GraphReferenceProperty : GraphProperty
	{
		#region Fields

		GraphType propertyType;
		bool isList;

		#endregion

		#region Constructors

		internal GraphReferenceProperty(GraphType declaringType, string name, int index, GraphType propertyType, bool isList)
			: base(declaringType, name, index)
		{
			this.propertyType = propertyType;
			this.isList = isList;
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

		#endregion
	}
}
