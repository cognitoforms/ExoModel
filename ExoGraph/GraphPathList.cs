namespace ExoGraph
{
	/// <summary>
	/// Exposes a list of <see cref="GraphPath"/> instances keyed by name.
	/// </summary>
	public class GraphPathList : ReadOnlyList<GraphPath>
	{
		protected override string GetName(GraphPath item)
		{
			return item.ToString();
		}
	}
}
