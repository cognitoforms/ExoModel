namespace ExoModel
{
	/// <summary>
	/// Exposes a list of <see cref="ModelPath"/> instances keyed by name.
	/// </summary>
	public class ModelPathList : ReadOnlyList<ModelPath>
	{
		protected override string GetName(ModelPath item)
		{
			return item.ToString();
		}
	}
}
