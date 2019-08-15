using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace ExoModel
{
	/// <summary>
	/// Container class which tracks <see cref="ModelType"/>'s and related model
	/// information for a single thread of execution.
	/// </summary>
	/// <remarks>
	/// Use <see cref="ModelContext.Current"/> to access the current context.
	/// <see cref="ModelContext.Provider"/> must be set in order to create and cache
	/// <see cref="ModelContext"/> instances.  <see cref="ModelContext.Init"/> can be
	/// called to perform standard initialization of <see cref="ModelContext"/> using
	/// the default context provider.
	/// </remarks>
	public sealed class ModelContext
	{
		#region Fields
		/// <summary>
		/// All extentions associated with the context
		/// </summary>
		Dictionary<Type, object> extensions;

		/// <summary>
		/// Tracks the types of objects in the model.
		/// </summary>
		ModelTypeList modelTypes = new ModelTypeList();

		/// <summary>
		/// Tracks providers registered to create <see cref="ModelType"/> instances.
		/// </summary>
		List<IModelTypeProvider> typeProviders = new List<IModelTypeProvider>();

		/// <summary>
		/// Tracks the last auto-generated id that was assigned to a new instance that is cached.
		/// </summary>
		static int lastCachedInstanceId;

		/// <summary>
		/// Tracks the last auto-generated id that was assigned to a new instance.
		/// </summary>
		int lastNonCachedInstanceId;

		/// <summary>
		/// Queue to store a FIFO list of types to be initialized
		/// </summary>
		Queue<ModelType> uninitialized = new Queue<ModelType>();

		/// <summary>
		/// Flag to indicate whether or not an initialization scope is in effect
		/// </summary>
		bool initializing = false;

		/// <summary>
		/// List of model types that have been initialized
		/// </summary>
		IList<ModelType> initialized = new List<ModelType>();
		#endregion

		#region Constructors

		/// <summary>
		/// Constructs a new <see cref="ModelContext"/>, using the specified <see cref="IModelTypeProvider"/>
		/// implementations to create <see cref="ModelType"/>'s for the context.
		/// </summary>
		/// <param name="providers"></param>
		public ModelContext(params IModelTypeProvider[] providers)
		{
			if (providers == null)
				return;

			foreach (IModelTypeProvider provider in providers)
				AddModelTypeProvider(provider);

			// Notify subscribers that a new context was created
			if (ContextInit != null)
				ContextInit(this, EventArgs.Empty);
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the current model context.
		/// </summary>
		public static ModelContext Current
		{
			get { return Provider.Context; }
			set { Provider.Context = value; }
		}

		/// <summary>
		/// Gets or sets the <see cref="IModelContextProvider"/> provider responsible for
		/// creating and storing the <see cref="ModelContext"/> for the application.
		/// </summary>
		public static IModelContextProvider Provider { get; set; }

		#endregion

		#region Events

		/// <summary>
		/// Notifies when all <see cref="ModelEvent"/> occurrences are raised within the current model context.
		/// </summary>
		public event EventHandler<ModelEvent> Event;

		/// <summary>
		/// Notifies when new types are initialized within the current <see cref="ModelContext"/>.
		/// </summary>
		public event EventHandler<TypeInitEventArgs> TypeInit;

		/// <summary>
		/// Notifies when a new <see cref="ModelContext"/> is initialized.
		/// </summary>
		public static event EventHandler ContextInit;

		#endregion

		#region Model Context Methods

		/// <summary>
		/// Provides a default implementation for initializing and caching <see cref="ModelContext"/>
		/// instances using the <see cref="ModelContextProvider"/> implementation of <see cref="IModelContextProvider"/>.
		/// </summary>
		/// <param name="contextInit"></param>
		/// <param name="providers"></param>
		public static void Init(Action contextInit, params IModelTypeProvider[] providers)
		{
			new ModelContextProvider().CreateContext +=
				(source, args) =>
				{
					// Create the new context
					args.Context = new ModelContext(providers);

					// Perform initialization after the context has been assigned
					if (contextInit != null)
						contextInit();
				};
		}
		/// <summary>
		/// Provides a default implementation for initializing and caching <see cref="ModelContext"/>
		/// instances using the <see cref="ModelContextProvider"/> implementation of <see cref="IModelContextProvider"/>.
		/// </summary>
		/// <param name="providers"></param>
		public static void Init(params IModelTypeProvider[] providers)
		{
			Init(null, providers);
		}

		/// <summary>
		/// Gets or creates an extension instance linked to the current <see cref="ModelContext"/>.
		/// </summary>
		/// <typeparam name="TExtension">The type of extension to create.</typeparam>
		/// <returns></returns>
		public TExtension GetExtension<TExtension>()
			where TExtension : class, new()
		{
			object extension;
			if (extensions == null)
				extensions = new Dictionary<Type, object>();
			if (!extensions.TryGetValue(typeof(TExtension), out extension))
				extensions[typeof(TExtension)] = extension = new TExtension();
			return (TExtension)extension;
		}

		/// <summary>
		/// Adds the current context back to the pool
		/// </summary>
		public static void EnsureReleased()
		{
			if (ModelContext.Provider != null && ModelContext.Current != null)
				((ModelContextProvider)ModelContext.Provider).ContextPool.Put(ModelContext.Current);
		}
		#endregion

		#region Model Instance Methods

		/// <summary>
		/// Called by each <see cref="ModelEvent"/> to notify the context that a model event has occurred.
		/// </summary>
		/// <param name="modelEvent"></param>
		internal void Notify(ModelEvent modelEvent)
		{
			if (Event != null)
				Event(this, modelEvent);
		}

		/// <summary>
		/// Generates a unique identifier to assign to new instances that do not yet have an id.
		/// </summary>
		/// <param name="isCached"></param>
		/// <returns></returns>
		internal string GenerateId(bool isCached)
		{
			return isCached ? ("~" + ++lastCachedInstanceId) : ("?" + ++lastNonCachedInstanceId);
		}

		/// <summary>
		/// Resets the current context in preparation for being reused after being cached in a context pool.
		/// </summary>
		internal void Reset()
		{
			lastNonCachedInstanceId = 0;
		}

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> associated with the specified real instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public ModelInstance GetModelInstance(object instance)
		{
			var type = GetModelType(instance);
			return type != null ? type.GetModelInstance(instance) : null;
		}

		/// <summary>
		/// Creates a new instance of the specified type.
		/// </summary>
		/// <typeparam name="TType"></typeparam>
		/// <returns></returns>
		public static TType Create<TType>()
		{
			return (TType)Current.GetModelType<TType>().Create().Instance;
		}

		/// <summary>
		/// Creates an existing instance of the specified type.
		/// </summary>
		/// <typeparam name="TType"></typeparam>
		/// <param name="id"></param>
		/// <returns></returns>
		public static TType Create<TType>(string id)
		{
			return (TType)Current.GetModelType<TType>().Create(id).Instance;
		}

		/// <summary>
		/// Creates a new instance of the specified type.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object Create(Type type)
		{
			return Current.GetModelType(type).Create().Instance;
		}

		/// <summary>
		/// Creates an existing instance of the specified type.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		public static object Create(Type type, string id)
		{
			return Current.GetModelType(type).Create(id).Instance;
		}

		/// <summary>
		/// Creates a new instance of the specified type.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		public static object Create(string typeName)
		{
			return Current.GetModelType(typeName).Create().Instance;
		}

		/// <summary>
		/// Creates an existing instance of the specified type.
		/// </summary>
		/// <param name="typeName"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		public static object Create(string typeName, string id)
		{
			return Current.GetModelType(typeName).Create(id).Instance;
		}

		#endregion

		#region Model Type Methods

		/// <summary>
		/// Gets the <see cref="ModelType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public ModelType GetModelType(string typeName)
		{
			// Return null if the type name is null
			if (typeName == null)
				return null;

			// Retrieve the cached model type
			ModelType type = modelTypes[typeName];
			if (type == null)
			{
				// Only perform initialization on non-recursive calls to this method
				bool initialize = initializing == false ? initializing = true : false;

				try
				{
					// Attempt to create the model type if it is not cached
					foreach (var provider in typeProviders)
					{
						// Allow initialization of cacheable types when attempting to load non-cachable types
						if (initializing && !provider.IsCachable)
						{
							try
							{
								initializing = false;
								type = provider.CreateModelType(typeName);
							}
							finally
							{
								initializing = true;
							}
						}
						else
							type = provider.CreateModelType(typeName);
						if (type != null)
						{
							if (type.Provider == null)
								type.Provider = provider;

							break;
						}
					}

					// Return null to indicate that the model type could not be created
					if (type == null)
						return null;

					// Register the new model type with the context
					modelTypes.Add(type);

					// Register the new model type to be initialized
					uninitialized.Enqueue(type);

					// Perform initialization if not recursing
					if (initialize)
					{
						// Initialize new model types in FIFO order
						while (uninitialized.Count > 0)
						{
							// Initialize the type
							var initType = uninitialized.Peek();

							// Handle exceptions that are thrown as a result of initializing the type. If an
							// error occurs, restore the model types list to a valid state and remove the set
							// of uninitialized model types. This is needed to ensure that the context is 
							// valid should it be assigned to another thread in the future.
							try
							{
								initType.Initialize(this);
							}
							catch (Exception e)
							{
								// Recreate the model types list based on only initialized types
								modelTypes = new ModelTypeList(modelTypes.Where(t => !uninitialized.Contains(t)).ToArray());

								// Discard all uninitialized model types
								uninitialized.Clear();

								throw e;
							}

							// Record the initialized type and remove from list of types needing initialization
							initialized.Add(initType);
							uninitialized.Dequeue();
						}

						initializing = false;
						initialize = false;

						// Raise event for all initialized types
						if (TypeInit != null)
						{
							var initializedTypeArray = initialized.ToArray();
							initialized.Clear();

							TypeInit(this, new TypeInitEventArgs(initializedTypeArray));
						}
					}
				}
				finally
				{
					// Ensure type is removed from the cache if it is not cachable
					if (type != null && !type.IsCachable)
						modelTypes.Remove(type);

					// If an error occurred during initilization, the initializing flag must be reset
					if (initialize)
						initializing = false;
				}
			}

			// Return the requested model type
			return type;
		}

		/// <summary>
		/// Gets the <see cref="ModelType"/> that corresponds to TType.
		/// </summary>
		/// <typeparam name="TType"></typeparam>
		/// <returns></returns>
		public ModelType GetModelType<TType>()
		{
			return GetModelType(typeof(TType));
		}

		/// <summary>
		/// Gets the <see cref="ModelType"/> that corresponds to the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public ModelType GetModelType(Type type)
		{
			// Walk up the inheritance hierarchy to find a model type for the specified .NET type
			do
			{
				var modelType = GetModelType
				(
					(from provider in typeProviders
					 let typeName = provider.GetModelTypeName(type)
					 where typeName != null
					 select typeName).FirstOrDefault()
				 );
				if (modelType != null)
					return modelType;
				type = type.BaseType;
			}
			while (type != null);

			return null;
		}

		/// <summary>
		/// Gets the <see cref="ModelType"/> that corresponds to the specified instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public ModelType GetModelType(object instance)
		{
			return GetModelType
			(
				(from provider in typeProviders
				 let typeName = provider.GetModelTypeName(instance)
				 where typeName != null
				 select typeName).FirstOrDefault()
			 );
		}

		/// <summary>
		/// Adds a new <see cref="IModelTypeProvider"/> to the set of providers used to resolve
		/// and create new <see cref="ModelType"/> instances.  
		/// </summary>
		/// <param name="typeProvider">The <see cref="IModelTypeProvider"/> to add</param>
		/// <remarks>
		/// Providers added last will be given precedence over previously added providers.
		/// </remarks>
		public void AddModelTypeProvider(IModelTypeProvider typeProvider)
		{
			typeProviders.Insert(0, typeProvider);
		}

		#endregion
	}
}
