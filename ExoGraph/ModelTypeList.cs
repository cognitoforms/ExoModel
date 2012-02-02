using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;

namespace ExoModel
{
	/// <summary>
	/// Exposes a list of <see cref="ModelType"/> instances keyed by name.
	/// </summary>
	public class ModelTypeList : ReadOnlyList<ModelType>
	{
		public ModelTypeList()
			: base()
		{
		}

		internal ModelTypeList(IEnumerable<ModelType> modelTypes)
			: base(modelTypes)
		{
		}

		protected override string GetName(ModelType item)
		{
			return item.Name;
		}
	}
}
