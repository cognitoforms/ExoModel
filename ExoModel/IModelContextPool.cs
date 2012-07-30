using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	/// <summary>
	/// Interface for pooling ModelContexts.  Implementations must be thread safe.
	/// </summary>
	public interface IModelContextPool
	{
		/// <summary>
		/// Removes and returns a context from the pool.
		/// </summary>
		/// <returns>A pooled context.  If the pool is empty, null is returned.</returns>
		ModelContext Get();

		/// <summary>
		/// Places an context into the pool
		/// </summary>
		/// <param name="context">Context that can be reused.</param>
		void Put(ModelContext context);

		/// <summary>
		/// Clears all items from the pool
		/// </summary>
		void Clear();

		/// <summary>
		/// Number of contexts in the pool
		/// </summary>
		int Count { get; }
	}
}
