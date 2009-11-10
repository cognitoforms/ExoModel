using System;

namespace ExoGraph
{
	/// <summary>
	/// Represents and tracks the scope of an event within the graph.
	/// </summary>
	public class GraphEventScope : IDisposable
	{
		#region Fields

		[ThreadStatic]
		static GraphEventScope current;

		GraphEventScope parent;
		GraphEvent @event;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="GraphEventScope"/> that represents a generic event
		/// within the object graph.
		/// </summary>
		public GraphEventScope()
		{
			parent = current;
			current = this;
		}

		/// <summary>
		/// Creates a new <see cref="GraphEventScope"/> that represents a specific event
		/// within the object graph.
		/// </summary>
		/// <param name="property"></param>
		public GraphEventScope(GraphEvent @event)
			: this()
		{
			this.@event = @event;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the type of reference that was modified as a result of the change
		/// </summary>
		public GraphEvent Event
		{
			get
			{
				return @event;
			}
		}

		/// <summary>
		/// Gets the current <see cref="GraphEventScope"/>.
		/// </summary>
		public static GraphEventScope Current
		{
			get
			{
				return current;
			}
		}

		/// <summary>
		/// Gets the parent <see cref="GraphEventScope"/> for this scope.
		/// </summary>
		public GraphEventScope Parent
		{
			get
			{
				return parent;
			}
		}

		/// <summary>
		/// Notifies subscribers when the outermost <see cref="GraphEventScope"/> has exited.
		/// </summary>
		public event EventHandler<GraphEventScopeExitedEventArgs> Exited;

		#endregion

		#region IDisposable Members

		/// <summary>
		/// Invokes the <see cref="Exited"/> event if this is the outermost <see cref="GraphEventScope"/>.
		/// </summary>
		void IDisposable.Dispose()
		{
			try
			{
				if (Exited != null)
				{
					if (parent != null)
						parent.Exited += Exited;
					else
					{
						// Raise the event in a loop to catch event subscriptions that occur while raising the event
						while (Exited != null)
						{
							EventHandler<GraphEventScopeExitedEventArgs> exited = Exited;
							Exited = null;
							exited(this, new GraphEventScopeExitedEventArgs(this));
						}
					}
				}
			}
			finally
			{
				// Set the current scope to the parent scope
				current = parent;
			}
		}

		#endregion
	}
}
