namespace ExoGraph
{
	/// <summary>
	/// Exposes a list of <see cref="GraphReferenceProperty"/> instances keyed by name.
	/// </summary>
	public class GraphReferencePropertyList : ReadOnlyList<GraphReferenceProperty>
	{
		protected override string GetName(GraphReferenceProperty item)
		{
			return item.Name;
		}
	}
}
