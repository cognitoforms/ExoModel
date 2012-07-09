using System;
using System.Linq;
using System.Web;
using System.Collections.Generic;
using System.Threading;

namespace ExoModel
{
	/// <summary>
	/// Implementation of <see cref="IModelContextProvider"/> that initializes a thread
	/// or web-request scoped <see cref="ModelContext"/> subclass, and allows subclasses
	/// to perform additional initialization work when new contexts are created.
	/// </summary>
	public class ModelContextProvider : IModelContextProvider
	{
		HashSet<ModelContext> pool = new HashSet<ModelContext>();

		[ThreadStatic]
		static Storage context;

		/// <summary>
		/// Creates a new <see cref="ModelContextProvider"/> and automatically assigns the instance
		/// as the current <see cref="ModelContext.Provider"/> implementation.
		/// </summary>
		public ModelContextProvider()
		{
			ModelContext.Provider = this;
		}

		/// <summary>
		/// Gets or sets the current <see cref="ModelContext"/>.
		/// </summary>
		public ModelContext Context
		{
			get
			{
				ModelContext context = GetStorage().Context;
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
		/// Adds the specified <see cref="ModelContext"/> to the context pool.
		/// </summary>
		/// <param name="context">The <see cref="ModelContext"/> to add.</param>
		/// <returns>True if the context was added to the pool, false if the context was already present.</returns>
		public static bool AddToPool(ModelContext context)
		{
			var provider = (ModelContextProvider)ModelContext.Provider;
			lock (provider.pool)
			{
				return provider.pool.Add(context);
			}
		}

		/// <summary>
		/// Removes all <see cref="ModelContext"/> instances from the pool.
		/// </summary>
		public static void FlushPool()
		{
			var provider = (ModelContextProvider)ModelContext.Provider;
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
		/// Event which allows subscribers to create a new <see cref="ModelContext"/> on behalf
		/// of the current <see cref="ModelContextProvider"/>.
		/// </summary>
		public event EventHandler<CreateContextEventArgs> CreateContext;

		/// <summary>
		/// Base implementation that creates a new <see cref="ModelContext"/> instance.
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
		/// Gets thread static or <see cref="HttpContext"/> storage for the <see cref="ModelContext"/>.
		/// </summary>
		/// <returns></returns>
		Storage GetStorage()
		{
			HttpContext webCtx = HttpContext.Current;

			// If in a web request, store the reference in HttpContext
			if (webCtx != null)
			{
				Storage storage = (Storage)webCtx.Items[typeof(ModelContextProvider)];

				if (storage == null)
					webCtx.Items[typeof(ModelContextProvider)] = storage = new Storage();

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
			ModelContextProvider provider;

			/// <summary>
			/// Creates a new <see cref="CreateContextEventArgs"/> which allows
			/// event subscribers to create a new <see cref="ModelContext"/>.
			/// </summary>
			/// <param name="provider"></param>
			public CreateContextEventArgs(ModelContextProvider provider)
			{
				this.provider = provider;
			}

			/// <summary>
			/// Gets or sets the <see cref="ModelContext"/> for the current provider.
			/// </summary>
			public ModelContext Context
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
			public ModelContext Context { get; set; }
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
				Storage storage = (Storage)((HttpApplication)sender).Context.Items[typeof(ModelContextProvider)];

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