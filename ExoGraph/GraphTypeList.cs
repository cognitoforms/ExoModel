using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;

namespace ExoGraph
{
	/// <summary>
	/// Exposes a list of <see cref="GraphType"/> instances keyed by name.
	/// </summary>
	public class GraphTypeList : ReadOnlyList<GraphType>
	{
		public GraphTypeList()
			: base()
		{
		}

		internal GraphTypeList(IEnumerable<GraphType> graphTypes)
			: base(graphTypes)
		{
		}

		protected override string GetName(GraphType item)
		{
			return item.Name;
		}
	}
}
