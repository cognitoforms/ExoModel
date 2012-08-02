using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	/// <summary>
	/// Default implementation of IModelContextPool
	/// </summary>
	public class ModelContextDefaultPool : IModelContextPool
	{
		HashSet<ModelContext> pool = new HashSet<ModelContext>();

		ModelContext IModelContextPool.Get()
		{
			ModelContext context = null;

			lock (pool)
			{
				if (pool.Count > 0)
				{
					context = pool.First();
					pool.Remove(context);
				}
			}

			return context;
		}

		void IModelContextPool.Put(ModelContext context)
		{
			lock (pool)
			{
				pool.Add(context);
			}
		}

		void IModelContextPool.Clear()
		{
			lock (pool)
			{
				pool.Clear();
			}
		}

		void IModelContextPool.EnsureContexts(int minimumNumber, Func<ModelContext> createContext)
		{
			lock (pool)
			{
				while (minimumNumber - pool.Count > 0)
				{
					pool.Add(createContext());
				}
			}
		}
	}
}
