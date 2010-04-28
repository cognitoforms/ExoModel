using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace ExoGraph
{
	/// <summary>
	/// Base class for context classes tracking the type information and events
	/// for a set of objects in graph.
	/// </summary>
	public sealed class GraphContext
	{
		#region Fields

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

		#endregion

		#region Constructors

		public GraphContext()
		{
		}

		public GraphContext(params IGraphTypeProvider[] providers)
		{
			foreach (IGraphTypeProvider provider in providers)
				AddGraphTypeProvider(provider);
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

		/// <summary>
		/// Indicates whether <see cref="GraphPropertyGetEvent"/> notifications should not be
		/// raised when a property is accessed.
		/// </summary>
		internal bool ShouldSuspendGetNotifications { get; private set; }

		#endregion

		#region Events

		/// <summary>
		/// Allows subscribers to be notified of all <see cref="GraphEvent"/> occurrences
		/// raised within the current graph context.
		/// </summary>
		public event EventHandler<GraphEvent> Event;

		#endregion

		#region Graph Instance Methods

		/// <summary>
		/// Begins a transaction within the current graph context.
		/// </summary>
		/// <returns>The transaction instance</returns>
		/// <remarks>
		/// The transaction subscribes to graph events and should be used inside a using block
		/// to ensure that the subscriptions are eventually released.
		/// <see cref="GraphTransaction.Commit"/> must be called to ensure the transaction is not rolled back.
		/// <see cref="Rollback"/> may be called at any time to force the transaction to roll back.
		/// After <see cref="Commit"/> or <see cref="Rollback"/> occurs, further graph events
		/// will not be tracked by the transaction.
		/// </remarks>
		public GraphTransaction BeginTransaction()
		{
			return new GraphTransaction(this);
		}

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

		protected internal IDisposable SuspendGetNotifications()
		{
			return new GetNotificationSuspension(this);
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
						uninitialized.Peek().Initialize(this);
						uninitialized.Dequeue();
					}
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
			return GetGraphType
			(
				(from provider in typeProviders
				 let typeName = provider.GetGraphTypeName(type)
				 where typeName != null
				 select typeName).FirstOrDefault()
			 );
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

		#region GetNotificationSuspension

		class GetNotificationSuspension : IDisposable
		{
			GraphContext context;

			internal GetNotificationSuspension(GraphContext context)
			{
				this.context = context;
				context.ShouldSuspendGetNotifications = true;
			}

			void IDisposable.Dispose()
			{
				context.ShouldSuspendGetNotifications = false;
			}
		}

		#endregion
	}
}
