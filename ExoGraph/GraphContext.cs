using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace ExoGraph
{
	/// <summary>
	/// Container class which tracks <see cref="GraphType"/>'s and related graph
	/// information for a single thread of execution.
	/// </summary>
	/// <remarks>
	/// Use <see cref="GraphContext.Current"/> to access the current context.
	/// <see cref="GraphContext.Provider"/> must be set in order to create and cache
	/// <see cref="GraphContext"/> instances.  <see cref="GraphContext.Init"/> can be
	/// called to perform standard initialization of <see cref="GraphContext"/> using
	/// the default context provider.
	/// </remarks>
	public sealed class GraphContext
	{
		#region Fields
		/// <summary>
		/// All extentions associated with the context
		/// </summary>
		Dictionary<Type, object> extensions;

		/// <summary>
		/// Tracks the types of objects in the graph.
		/// </summary>
		GraphTypeList graphTypes = new GraphTypeList();

		/// <summary>
		/// Tracks providers registered to create <see cref="GraphType"/> instances.
		/// </summary>
		List<IGraphTypeProvider> typeProviders = new List<IGraphTypeProvider>();

		/// <summary>
		/// Tracks the next auto-generated id assigned to new instances.
		/// </summary>
		int nextId;

		/// <summary>
		/// Queue to store a FIFO list of types to be initialized
		/// </summary>
		Queue<GraphType> uninitialized = new Queue<GraphType>();

		/// <summary>
		/// Flag to indicate whether or not an initialization scope is in effect
		/// </summary>
		bool initializing = false;

		/// <summary>
		/// List of graph types that have been initialized
		/// </summary>
		IList<GraphType> initialized = new List<GraphType>();
		#endregion

		#region Constructors

		/// <summary>
		/// Constructs a new <see cref="GraphContext"/>, using the specified <see cref="IGraphTypeProvider"/>
		/// implementations to create <see cref="GraphType"/>'s for the context.
		/// </summary>
		/// <param name="providers"></param>
		public GraphContext(params IGraphTypeProvider[] providers)
		{
			if (providers == null)
				return;

			foreach (IGraphTypeProvider provider in providers)
				AddGraphTypeProvider(provider);

			// Notify subscribers that a new context was created
			if (ContextInit != null)
				ContextInit(this, EventArgs.Empty);
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the current graph context.
		/// </summary>
		public static GraphContext Current
		{
			get { return Provider.Context; }
			set { Provider.Context = value; }
		}

		/// <summary>
		/// Gets or sets the <see cref="IGraphContextProvider"/> provider responsible for
		/// creating and storing the <see cref="GraphContext"/> for the application.
		/// </summary>
		public static IGraphContextProvider Provider { get; set; }

		#endregion

		#region Events

		/// <summary>
		/// Notifies when all <see cref="GraphEvent"/> occurrences are raised within the current graph context.
		/// </summary>
		public event EventHandler<GraphEvent> Event;

		/// <summary>
		/// Notifies when new types are initialized within the current graph context.
		/// </summary>
		public event EventHandler<TypeInitEventArgs> TypeInit;

		/// <summary>
		/// Notifies when a new <see cref="GraphContext"/> is initialized.
		/// </summary>
		public static event EventHandler ContextInit;

		#endregion

		#region Graph Context Methods

		/// <summary>
		/// Provides a default implementation for initializing and caching <see cref="GraphContext"/>
		/// instances using the <see cref="GraphContextProvider"/> implementation of <see cref="IGraphContextProvider"/>.
		/// </summary>
		/// <param name="createContext"></param>
		public static void Init(Action contextInit, params IGraphTypeProvider[] providers)
		{
			new GraphContextProvider().CreateContext +=
				(source, args) =>
				{
					// Create the new context
					args.Context = new GraphContext(providers);

					// Perform initialization after the context has been assigned
					if (contextInit != null)
						contextInit();
				};
		}
		/// <summary>
		/// Provides a default implementation for initializing and caching <see cref="GraphContext"/>
		/// instances using the <see cref="GraphContextProvider"/> implementation of <see cref="IGraphContextProvider"/>.
		/// </summary>
		/// <param name="createContext"></param>
		public static void Init(params IGraphTypeProvider[] providers)
		{
			Init(null, providers);
		}

		/// <summary>
		/// Gets or creates an extension instance linked to the current <see cref="GraphContext"/>.
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
		#endregion

		#region Graph Instance Methods

		/// <summary>
		/// Called by each <see cref="GraphEvent"/> to notify the context that a graph event has occurred.
		/// </summary>
		/// <param name="graphEvent"></param>
		internal void Notify(GraphEvent graphEvent)
		{
			if (Event != null)
				Event(this, graphEvent);
		}

		/// <summary>
		/// Generates a unique identifier to assign to new instances that do not yet have an id.
		/// </summary>
		/// <returns></returns>
		internal string GenerateId()
		{
			return "?" + ++nextId;
		}

		/// <summary>
		/// Resets the current context in preparation for being reused after being cached in a context pool.
		/// </summary>
		internal void Reset()
		{
			nextId = 0;
		}

		/// <summary>
		/// Gets the <see cref="GraphInstance"/> associated with the specified real instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public GraphInstance GetGraphInstance(object instance)
		{
			var type = GetGraphType(instance);
			return type != null ? type.GetGraphInstance(instance) : null;
		}

		/// <summary>
		/// Creates a new instance of the specified type.
		/// </summary>
		/// <typeparam name="TType"></typeparam>
		/// <returns></returns>
		public static TType Create<TType>()
		{
			return (TType)Current.GetGraphType<TType>().Create().Instance;
		}

		/// <summary>
		/// Creates an existing instance of the specified type.
		/// </summary>
		/// <typeparam name="TType"></typeparam>
		/// <param name="id"></param>
		/// <returns></returns>
		public static TType Create<TType>(string id)
		{
			return (TType)Current.GetGraphType<TType>().Create(id).Instance;
		}

		/// <summary>
		/// Creates a new instance of the specified type.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		public static object Create(string typeName)
		{
			return Current.GetGraphType(typeName).Create().Instance;
		}

		/// <summary>
		/// Creates an existing instance of the specified type.
		/// </summary>
		/// <param name="typeName"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		public static object Create(string typeName, string id)
		{
			return Current.GetGraphType(typeName).Create(id).Instance;
		}

		#endregion

		#region Graph Type Methods

		/// <summary>
		/// Gets the <see cref="GraphType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public GraphType GetGraphType(string typeName)
		{
			// Return null if the type name is null
			if (typeName == null)
				return null;

			// Retrieve the cached graph type
			GraphType type = graphTypes[typeName];
			if (type == null)
			{
				// Only perform initialization on non-recursive calls to this method
				bool initialize = initializing == false ? initializing = true : false;

				try
				{
					// Attempt to create the graph type if it is not cached
					foreach (var provider in typeProviders)
					{
						type = provider.CreateGraphType(typeName);
						if (type != null)
						{
							type.Provider = provider;
							break;
						}
					}

					// Return null to indicate that the graph type could not be created
					if (type == null)
						return null;

					// Register the new graph type with the context
					graphTypes.Add(type);

					// Register the new graph type to be initialized
					uninitialized.Enqueue(type);

					// Perform initialization if not recursing
					if (initialize)
					{
						// Initialize new graph types in FIFO order
						while (uninitialized.Count > 0)
						{
							// Initialize the type
							var initType = uninitialized.Peek();

							// Handle exceptions that are thrown as a result of initializing the type. If an
							// error occurs, restore the graph types list to a valid state and remove the set
							// of uninitialized graph types. This is needed to ensure that the context is 
							// valid should it be assigned to another thread in the future.
							try
							{
								initType.Initialize(this);
							}
							catch (Exception e)
							{
								// Recreate the graph types list based on only initialized types
								graphTypes = new GraphTypeList(graphTypes.Where(t => !uninitialized.Contains(t)).ToArray());

								// Discard all uninitialized graph types
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
					// If an error occurred during initilization, the initializing flag must be reset
					if (initialize)
						initializing = false;
				}
			}

			// Return the requested graph type
			return type;
		}

		/// <summary>
		/// Gets the <see cref="GraphType"/> that corresponds to TType.
		/// </summary>
		/// <typeparam name="TType"></typeparam>
		/// <returns></returns>
		public GraphType GetGraphType<TType>()
		{
			return GetGraphType(typeof(TType));
		}

		/// <summary>
		/// Gets the <see cref="GraphType"/> that corresponds to the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public GraphType GetGraphType(Type type)
		{
			// Walk up the inheritance hierarchy to find a graph type for the specified .NET type
			do
			{
				var graphType = GetGraphType
				(
					(from provider in typeProviders
					 let typeName = provider.GetGraphTypeName(type)
					 where typeName != null
					 select typeName).FirstOrDefault()
				 );
				if (graphType != null)
					return graphType;
				type = type.BaseType;
			}
			while (type != null);

			return null;
		}

		/// <summary>
		/// Gets the <see cref="GraphType"/> that corresponds to the specified instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public GraphType GetGraphType(object instance)
		{
			return GetGraphType
			(
				(from provider in typeProviders
				 let typeName = provider.GetGraphTypeName(instance)
				 where typeName != null
				 select typeName).FirstOrDefault()
			 );
		}

		/// <summary>
		/// Adds a new <see cref="IGraphTypeProvider"/> to the set of providers used to resolve
		/// and create new <see cref="GraphType"/> instances.  
		/// </summary>
		/// <param name="typeProvider">The <see cref="IGraphTypeProvider"/> to add</param>
		/// <remarks>
		/// Providers added last will be given precedence over previously added providers.
		/// </remarks>
		public void AddGraphTypeProvider(IGraphTypeProvider typeProvider)
		{
			typeProviders.Insert(0, typeProvider);
		}

		#endregion
	}
}
