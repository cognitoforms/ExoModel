using System.Collections.Generic;
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

		public ModelPropertyList(IEnumerable<ModelProperty> items)
			: base(items)
		{ }

		protected override string GetName(ModelProperty item)
		{
			return item.Name;
		}
	}
}
