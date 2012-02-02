using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	public class ModelMethodParameterList : ReadOnlyList<ModelMethodParameter>
	{
		internal ModelMethodParameterList()
			: base(parameter => parameter.Index)
		{ }

		protected override string GetName(ModelMethodParameter item)
		{
			return item.Name;
		}
	}
}
