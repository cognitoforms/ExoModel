namespace ExoModel
{
	/// <summary>
	/// Exposes a list of <see cref="ModelValueProperty"/> instances keyed by name.
	/// </summary>
	public class ModelValuePropertyList : ReadOnlyList<ModelValueProperty>
	{
		protected override string GetName(ModelValueProperty item)
		{
			return item.Name;
		}
	}
}
