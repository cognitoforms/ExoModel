using System;

namespace ExoModel
{
	/// <summary>
	/// Represents and tracks the scope of an event within the model.
	/// </summary>
	public class ModelEventScope
	{
		#region Fields

		[ThreadStatic]
		static ModelEventScope current;

		ModelEventScope parent;
		ModelEvent @event;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="ModelEventScope"/> that represents a generic event
		/// within the object model.
		/// </summary>
		internal ModelEventScope()
		{
			parent = current;
			current = this;
		}

		/// <summary>
		/// Creates a new <see cref="ModelEventScope"/> that represents a specific event
		/// within the object model.
		/// </summary>
		/// <param name="property"></param>
		ModelEventScope(ModelEvent @event)
			: this()
		{
			this.@event = @event;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the type of reference that was modified as a result of the change
		/// </summary>
		public ModelEvent Event
		{
			get
			{
				return @event;
			}
		}

		/// <summary>
		/// Gets the current <see cref="ModelEventScope"/>.
		/// </summary>
		public static ModelEventScope Current
		{
			get
			{
				return current;
			}
		}

		/// <summary>
		/// Gets the parent <see cref="ModelEventScope"/> for this scope.
		/// </summary>
		public ModelEventScope Parent
		{
			get
			{
				return parent;
			}
		}

		/// <summary>
		/// Notifies subscribers when the outermost <see cref="ModelEventScope"/> has exited.
		/// </summary>
		public event EventHandler<ModelEventScopeExitedEventArgs> Exited;

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
				EventHandler<ModelEventScopeExitedEventArgs> exited = Exited;
				Exited = null;
				exited(this, new ModelEventScopeExitedEventArgs(this));
			}
		}

		/// <summary>
		/// Performs the specified action inside a <see cref="ModelEventScope"/>.
		/// </summary>
		/// <param name="action"></param>
		public static void Perform(Action action)
		{
			new ModelEventScope().PerformAction(action);
		}

		/// <summary>
		/// Performs the specified action inside a <see cref="ModelEventScope"/>.
		/// </summary>
		/// <param name="action"></param>
		internal static void Perform(ModelEvent @event, Action action)
		{
			new ModelEventScope(@event).PerformAction(action);
		}

		/// <summary>
		/// Performs the specified action inside the current <see cref="ModelEventScope"/>.
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
		/// Causes the specified action to be performed when the outermost model event
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
		/// Invokes the <see cref="Exited"/> event if this is the outermost <see cref="ModelEventScope"/>.
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
		/// Represents an exception that occurs while prematurely exiting a model event scope 
		/// due to an exception that occurred within the scope of work.
		/// </summary>
		public class ScopeException : Exception
		{
			public ScopeException(Exception exit, Exception original)
				: base("An error occurring while performing an action inside a model event scope.", original)
			{
				this.Exit = exit;
				this.Original = original;
			}

			/// <summary>
			/// The exception that occurred while exiting the scope.
			/// </summary>
			public Exception Exit { get; private set; }

			/// <summary>
			/// The exception that occurred that triggered the scope to exit.
			/// </summary>
			public Exception Original { get; private set; }
		}

		#endregion
	}
}
