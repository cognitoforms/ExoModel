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
		/// Ensures that the specified number of contexts exist in the pool.
		/// </summary>
		/// <param name="minimumNumber">Number of contexts to ensure</param>
		/// <param name="createContext">Factory method to create a new context</param>
		void EnsureContexts(int minimumNumber, Func<ModelContext> createContext);

		/// <summary>
		/// Called after context creation.
		/// </summary>
		/// <param name="context"></param>
		void OnContextCreated(ModelContext context);
	}
}
