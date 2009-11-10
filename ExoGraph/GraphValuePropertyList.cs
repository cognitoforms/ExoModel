namespace ExoGraph
{
	/// <summary>
	/// Exposes a list of <see cref="GraphValueProperty"/> instances keyed by name.
	/// </summary>
	public class GraphValuePropertyList : ReadOnlyList<GraphValueProperty>
	{
		protected override string GetName(GraphValueProperty item)
		{
			return item.Name;
		}
	}
}
