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
		[ThreadStatic]
		static Storage context;

		/// <summary>
		/// Creates a new <see cref="ModelContextProvider"/> and automatically assigns the instance
		/// as the current <see cref="ModelContext.Provider"/> implementation.
		/// </summary>
		public ModelContextProvider()
		{
			ModelContext.Provider = this;
			
			// Defaults:
			ContextPool = new ModelContextDefaultPool();
		}

		/// <summary>
		/// Gets or sets the current <see cref="ModelContext"/>.
		/// </summary>
		public ModelContext Context
		{
			get
			{
				// has the context already be bound to this thread/request?
				Storage storage = GetStorage();
				ModelContext context = storage.Context;
				
				if (context == null)
				{
					if(storage.ContextReturnedToPool)
						throw new ApplicationException("Cannot fetch a context after returning it to the pool as it could result in a memory leak.");

					// Context has not yet be bound.
					// Try to get a context from the pool
					context = ContextPool.Get();

					if (context != null)
					{
						// Use a pooled context
						Context = context;
						context.Reset();
					}
					else
					{
						// Nothing in the pool. OnCreateContext will set storage.Context.
						OnCreateContext();
						context = storage.Context;
						ContextPool.OnContextCreated(context);

						if (context == null)
							throw new InvalidOperationException("The CreateContext event handler did not set a Context but should have.");
					}
				}
				return context;
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
		public static void AddToPool(ModelContext context)
		{
			var provider = (ModelContextProvider)ModelContext.Provider;
			provider.ContextPool.Put(context);
		}

		/// <summary>
		/// Removes all <see cref="ModelContext"/> instances from the pool.
		/// </summary>
		public static void FlushPool()
		{
			var provider = (ModelContextProvider)ModelContext.Provider;
			provider.ContextPool.Clear();
		}

		/// <summary>
		/// Specifies the minimum number of contexts that should
		/// be available in the pool or in use.
		/// </summary>
		public void EnsureContexts(int minimumNumber)
		{
			ModelContext originalContext = GetStorage().Context;

			try
			{
				var provider = (ModelContextProvider)ModelContext.Provider;
				provider.ContextPool.EnsureContexts(minimumNumber, () => { 
					OnCreateContext();
					return GetStorage().Context;
				});
			}
			finally
			{
				GetStorage().Context = originalContext;
			}
		}

		/// <summary>
		/// <see cref="IModelContextPool"/> that will be used to track and re-use contexts
		/// </summary>
		public IModelContextPool ContextPool {get; set;}

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
			internal ModelContext Context { get; set; }
			internal bool ContextReturnedToPool { get; private set; }

			internal void OnEndRequest()
			{
				if(Context == null)
					return;

				AddToPool(Context);
				Context = null;
				ContextReturnedToPool = true;
			}
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
				context.EndRequest += new EventHandler(context_EndRequest);
			}

			void context_EndRequest(object sender, EventArgs e)
			{
				Storage storage = (Storage)((HttpApplication)sender).Context.Items[typeof(ModelContextProvider)];

				if (storage != null)
					storage.OnEndRequest();
			}

			#endregion
		}

		#endregion
	}
}