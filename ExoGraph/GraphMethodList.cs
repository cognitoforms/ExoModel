using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoGraph
{
	public class GraphMethodList : ReadOnlyList<GraphMethod>
	{
		protected override string GetName(GraphMethod item)
		{
			return item.Name;
		}
	}
}
