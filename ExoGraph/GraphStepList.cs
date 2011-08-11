using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;

namespace ExoGraph
{
	/// <summary>
	/// Exposes a list of <see cref="GraphStep"/> instances keyed by name.
	/// </summary>
	public class GraphStepList : ReadOnlyList<GraphStep>
	{
		public GraphStepList()
			: base()
		{
		}

		internal GraphStepList(IEnumerable<GraphStep> graphSteps)
			: base(graphSteps)
		{
		}

		protected override string GetName(GraphStep item)
		{
			return item.Property.Name + "<" + (item.Filter != null ? item.Filter.Name : item.Property.DeclaringType.Name) + ">";
		}
	}
}
