using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoGraph
{
	public class GraphMethodParameterList : ReadOnlyList<GraphMethodParameter>
	{
		internal GraphMethodParameterList()
			: base(parameter => parameter.Index)
		{ }

		protected override string GetName(GraphMethodParameter item)
		{
			return item.Name;
		}
	}
}
