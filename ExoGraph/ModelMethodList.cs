using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	public class ModelMethodList : ReadOnlyList<ModelMethod>
	{
		protected override string GetName(ModelMethod item)
		{
			return item.Name;
		}
	}
}
