namespace ExoModel
{
	/// <summary>
	/// Exposes a list of <see cref="ModelProperty"/> instances keyed by name.
	/// </summary>
	public class ModelPropertyList : ReadOnlyList<ModelProperty>
	{
		public ModelPropertyList()
			: base(i => i.Index)
		{
		}

		protected override string GetName(ModelProperty item)
		{
			return item.Name;
		}
	}
}
