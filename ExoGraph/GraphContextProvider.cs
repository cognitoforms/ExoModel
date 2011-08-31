using System;
using System.Linq;
using System.Web;
using System.Collections.Generic;
using System.Threading;

namespace ExoGraph
{
	/// <summary>
	/// Implementation of <see cref="IGraphContextProvider"/> that initializes a thread
	/// or web-request scoped <see cref="GraphContext"/> subclass, and allows subclasses
	/// to perform additional initialization work when new contexts are created.
	/// </summary>
	public class GraphContextProvider : IGraphContextProvider
	{
		HashSet<GraphContext> pool = new HashSet<GraphContext>();

		[ThreadStatic]
		Storage context;

		/// <summary>
		/// Creates a new <see cref="GraphContextProvider"/> and automatically assigns the instance
		/// as the current <see cref="GraphContext.Provider"/> implementation.
		/// </summary>
		public GraphContextProvider()
		{
			GraphContext.Provider = this;
		}

		/// <summary>
		/// Gets or sets the current <see cref="GraphContext"/>.
		/// </summary>
		public GraphContext Context
		{
			get
			{
				GraphContext context = GetStorage().Context;
				if (context == null)
				{
					if (pool.Count > 0)
					{
						lock (pool)
						{
							if (pool.Count > 0)
							{
								Context = pool.First();
								Context.Reset();
								pool.Remove(Context);
							}
							else
							{
								OnCreateContext();
							}
						}
					}
					else
					{
						OnCreateContext();
					}
				}
				return GetStorage().Context;
			}
			set
			{
				GetStorage().Context = value;
			}
		}

		/// <summary>
		/// Adds the specified <see cref="GraphContext"/> to the context pool.
		/// </summary>
		/// <param name="context">The <see cref="GraphContext"/> to add.</param>
		/// <returns>True if the context was added to the pool, false if the context was already present.</returns>
		public static bool AddToPool(GraphContext context)
		{
			var provider = (GraphContextProvider)GraphContext.Provider;
			lock (provider.pool)
			{
				return provider.pool.Add(context);
			}
		}

		/// <summary>
		/// Removes all <see cref="GraphContext"/> instances from the pool.
		/// </summary>
		public static void FlushPool()
		{
			var provider = (GraphContextProvider)GraphContext.Provider;
			lock (provider.pool)
			{
				provider.pool.Clear();
			}
		}

		/// <summary>
		/// Specifies the minimum number of contexts that should
		/// be available in the pool or in use.
		/// </summary>
		public void EnsureContexts(int count)
		{
			for (int i = 0, difference = count - (pool.Count); i < difference; i++)
			{
				OnCreateContext();
				AddToPool(GetStorage().Context);
			}
		}

		/// <summary>
		/// Event which allows subscribers to create a new <see cref="GraphContext"/> on behalf
		/// of the current <see cref="GraphContextProvider"/>.
		/// </summary>
		public event EventHandler<CreateContextEventArgs> CreateContext;

		/// <summary>
		/// Base implementation that creates a new <see cref="GraphContext"/> instance.
		/// </summary>
		/// <returns>The new context</returns>
		/// <remarks>
		/// Subclasses may override <see cref="CreateContext"/> to perform additional context initialization
		/// or even implement a context pool to select existing contexts that are not currently in use.
		/// </remarks>
		protected virtual void OnCreateContext()
		{
			if (CreateContext != null)
				CreateContext(this, new CreateContextEventArgs(this));
		}

		/// <summary>
		/// Gets thread static or <see cref="HttpContext"/> storage for the <see cref="GraphContext"/>.
		/// </summary>
		/// <returns></returns>
		Storage GetStorage()
		{
			HttpContext webCtx = HttpContext.Current;

			// If in a web request, store the reference in HttpContext
			if (webCtx != null)
			{
				Storage storage = (Storage)webCtx.Items[typeof(GraphContextProvider)];

				if (storage == null)
					webCtx.Items[typeof(GraphContextProvider)] = storage = new Storage();

				return storage;
			}

			// Otherwise, store the reference in a thread static variable
			else
			{
				if (context == null)
					context = new Storage();

				return context;
			}
		}

		#region CreateContextEventArgs

		/// <summary>
		/// Event arguments for the <see cref="CreateContext"/> event.
		/// </summary>
		public class CreateContextEventArgs : EventArgs
		{
			GraphContextProvider provider;

			/// <summary>
			/// Creates a new <see cref="CreateContextEventArgs"/> which allows
			/// event subscribers to create a new <see cref="GraphContext"/>.
			/// </summary>
			/// <param name="provider"></param>
			public CreateContextEventArgs(GraphContextProvider provider)
			{
				this.provider = provider;
			}

			/// <summary>
			/// Gets or sets the <see cref="GraphContext"/> for the current provider.
			/// </summary>
			public GraphContext Context
			{
				get
				{
					return provider.Context;
				}
				set
				{
					provider.Context = value;
				}
			}
		}

		#endregion

		#region Storage

		/// <summary>
		/// Reference class used to provide storage for the context.
		/// </summary>
		class Storage
		{
			public GraphContext Context { get; set; }
		}

		#endregion

		#region PoolModule

		public class PoolModule : IHttpModule
		{
			#region IHttpModule Members

			void IHttpModule.Dispose()
			{
			}

			void IHttpModule.Init(HttpApplication context)
			{
				context.ReleaseRequestState += new EventHandler(context_ReleaseRequestState);
			}

			void context_ReleaseRequestState(object sender, EventArgs e)
			{
				Storage storage = (Storage)((HttpApplication)sender).Context.Items[typeof(GraphContextProvider)];

				if (storage != null)
				{
					AddToPool(storage.Context);
					storage.Context = null;
				}
			}

			#endregion
		}

		#endregion
	}
}