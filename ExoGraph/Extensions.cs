using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace ExoGraph
{
	public static class Extensions
	{
		public static IEnumerable<GraphType> GetGraphTypes(this Assembly assembly)
		{
			GraphContext context = GraphContext.Current;
			return assembly.GetTypes().Select(t => context.GetGraphType(t)).Where(t => t != null);
		}
	}
}
