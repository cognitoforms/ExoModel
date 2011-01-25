using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoGraph
{
	public interface IGraphTypeProvider
	{
		/// <summary>
		/// Gets the unique name of the <see cref="GraphType"/> for the specified graph object instance.
		/// </summary>
		/// <param name="instance">The actual graph object instance</param>
		/// <returns>The unique name of the graph type for the instance if it is a valid graph type, otherwise null</returns>
		string GetGraphTypeName(object instance);

		/// <summary>
		/// Gets the unique name of the <see cref="GraphType"/> that corresponds to the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		string GetGraphTypeName(Type type);

		/// <summary>
		/// Creates a <see cref="GraphType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		GraphType CreateGraphType(string typeName);

		/// <summary>
		/// Gets the fully-qualified name of the scope that the instance is currently in.
		/// </summary>
		string GetScopeName(GraphInstance instance);
	}
}
