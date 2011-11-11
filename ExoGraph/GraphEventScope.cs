using System;

namespace ExoGraph
{
	/// <summary>
	/// Represents and tracks the scope of an event within the graph.
	/// </summary>
	public class GraphEventScope
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
		internal GraphEventScope()
		{
			parent = current;
			current = this;
		}

		/// <summary>
		/// Creates a new <see cref="GraphEventScope"/> that represents a specific event
		/// within the object graph.
		/// </summary>
		/// <param name="property"></param>
		GraphEventScope(GraphEvent @event)
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

		#region Methods

		/// <summary>
		/// Raises the Exited event for the current scope and all parent scopes.
		/// </summary>
		public void Flush()
		{
			// flush parent scopes first
			if (parent != null)
				parent.Flush();

			RaiseExited();
		}

		private void RaiseExited()
		{
			// Raise the event in a loop to catch event subscriptions that occur while raising the event
			while (Exited != null)
			{
				EventHandler<GraphEventScopeExitedEventArgs> exited = Exited;
				Exited = null;
				exited(this, new GraphEventScopeExitedEventArgs(this));
			}
		}

		/// <summary>
		/// Performs the specified action inside a <see cref="GraphEventScope"/>.
		/// </summary>
		/// <param name="action"></param>
		public static void Perform(Action action)
		{
			new GraphEventScope().PerformAction(action);
		}

		/// <summary>
		/// Performs the specified action inside a <see cref="GraphEventScope"/>.
		/// </summary>
		/// <param name="action"></param>
		internal static void Perform(GraphEvent @event, Action action)
		{
			new GraphEventScope(@event).PerformAction(action);
		}

		/// <summary>
		/// Performs the specified action inside the current <see cref="GraphEventScope"/>.
		/// </summary>
		/// <param name="action"></param>
		void PerformAction(Action action)
		{
			try
			{
				action();
			}
			catch (Exception actionException)
			{
				try
				{
					Exit();
				}
				catch (Exception disposalException)
				{
					throw new ScopeException(disposalException, actionException);
				}

				throw;
			}
			Exit();
		}

		/// <summary>
		/// Causes the specified action to be performed when the outermost graph event
		/// scope has exited, or performs the action immediately if there is not a current scope.
		/// </summary>
		/// <param name="action"></param>
		public static void OnExit(Action action)
		{
			if (current == null)
				action();
			else
				current.Exited += (sender, e) => action();
		}

		/// <summary>
		/// Invokes the <see cref="Exited"/> event if this is the outermost <see cref="GraphEventScope"/>.
		/// </summary>
		internal void Exit()
		{
			try
			{
				if (Exited != null)
				{
					if (parent != null)
						parent.Exited += Exited;
					else
						RaiseExited();
				}
			}
			finally
			{
				// Set the current scope to the parent scope
				current = parent;
			}
		}

		#endregion

		#region ScopeException

		/// <summary>
		/// Represents an exception that occurs while prematurely exiting a graph event scope 
		/// due to an exception that occurred within the scope of work.
		/// </summary>
		public class ScopeException : Exception
		{
			public ScopeException(Exception exit, Exception original)
				: base("An error occurring while performing an action inside a graph event scope.", original)
			{
				this.Exit = exit;
				this.Original = original;
			}

			/// <summary>
			/// The exception that occurred while exiting the scope.
			/// </summary>
			public Exception Exit { get; private set; }

			/// <summary>
			/// The exception that occured that triggered the scope to exit.
			/// </summary>
			public Exception Original { get; private set; }
		}

		#endregion
	}
}
