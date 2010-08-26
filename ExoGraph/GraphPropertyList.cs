namespace ExoGraph
{
	/// <summary>
	/// Exposes a list of <see cref="GraphProperty"/> instances keyed by name.
	/// </summary>
	public class GraphPropertyList : ReadOnlyList<GraphProperty>
	{
		public GraphPropertyList()
			: base(i => i.Index)
		{
		}

		protected override string GetName(GraphProperty item)
		{
			return item.Name;
		}
	}
}
