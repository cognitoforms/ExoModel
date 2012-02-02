namespace ExoModel
{
	/// <summary>
	/// Exposes a list of <see cref="ModelReferenceProperty"/> instances keyed by name.
	/// </summary>
	public class ModelReferencePropertyList : ReadOnlyList<ModelReferenceProperty>
	{
		protected override string GetName(ModelReferenceProperty item)
		{
			return item.Name;
		}
	}
}
