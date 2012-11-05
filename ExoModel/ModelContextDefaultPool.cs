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
		readonly Stack<ModelContext> pool = new Stack<ModelContext>();

		ModelContext IModelContextPool.Get()
		{
			ModelContext context = null;

			lock (pool)
			{
				if (pool.Count > 0)
				{
					context = pool.Pop();
				}
			}

			return context;
		}

		void IModelContextPool.Put(ModelContext context)
		{
			lock (pool)
			{
				if(!pool.Contains(context))
					pool.Push(context);
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
				if (pool.Count < minimumNumber)
				{
					List<ModelContext> existing = pool.Reverse().ToList();
					pool.Clear();

					while (minimumNumber - pool.Count - existing.Count > 0)
					{
						pool.Push(createContext());
					}

					existing.ForEach(c => pool.Push(c));
				}
			}
		}

		public void OnContextCreated(ModelContext context)
		{
		}
	}
}
