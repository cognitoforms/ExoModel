using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;

namespace ExoGraph
{
	/// <summary>
	/// Tracks all <see cref="GraphEvent"/> occurrences within a context and allows changes
	/// to be recorded or rolled back entirely.
	/// </summary>
	public class GraphTransaction : IEnumerable<GraphEvent>, IDisposable
	{
		Dictionary<string, GraphInstance> newInstances;
		GraphContext context;

		List<GraphEvent> events = new List<GraphEvent>();

		/// <summary>
		/// Records <see cref="GraphEvent"/> occurences within the current context.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void context_Event(object sender, GraphEvent e)
		{
			// Only track events that change the graph
			if (e is ITransactedGraphEvent)
			{
				// Track id mappings for new instances created during the transaction
				if (e is GraphInitEvent.InitNew)
					RegisterNewInstance(e.Instance);

				// Populate save events with ids that have changed during the transaction
				if (e is GraphSaveEvent)
				{
					HashSet<GraphInstance> added = new HashSet<GraphInstance>();
					HashSet<GraphInstance> modified = new HashSet<GraphInstance>();
					HashSet<GraphInstance> deleted = new HashSet<GraphInstance>();
					
					for (var transaction = this; transaction != null; transaction = transaction.PreviousTransaction)
					{
						// Process the transaction log in reverse order, searching for instances 
						// that were persisted by the save event
						for (var i = transaction.events.Count - 1; i >= 0; i--)
						{
							var evt = transaction.events[i];

							// Stop processing if a previous save event is encountered
							if (evt is GraphSaveEvent)
								goto UpdateSave;

							// Deleted
							if (evt.Instance.IsDeleted)
								deleted.Add(evt.Instance);

							// Added
							else if (evt is GraphInitEvent.InitNew && !evt.Instance.IsNew)
								added.Add(evt.Instance);

							// Modified
							else if (!evt.Instance.IsNew && !evt.Instance.IsModified)
								modified.Add(evt.Instance);
								
						}
					}

		UpdateSave: GraphSaveEvent saveEvent = (GraphSaveEvent)e;
					saveEvent.Added = added.ToArray();
					saveEvent.Deleted = deleted.ToArray();
					saveEvent.Modified = modified.Except(saveEvent.Added).Except(saveEvent.Deleted).ToArray();
				}

				// Add the transacted event to the list of events for the transaction
				events.Add(e);
			}
		}

		GraphContext Context
		{
			get
			{
				if (context == null)
					context = GraphContext.Current;
				return context;
			}
		}

		public bool IsActive { get; private set; }

		public GraphTransaction PreviousTransaction { get; private set; }

		void RegisterNewInstance(GraphInstance instance)
		{
			if (newInstances == null)
				newInstances = new Dictionary<string,GraphInstance>();
			newInstances[instance.Type.Name + "|" + instance.Id] = instance;
		}

		/// <summary>
		/// Gets a <see cref="GraphInstance"/> with the specified type and id, which may be either an existing
		/// instance or a new instance created during the scope of the current transaction.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		public GraphInstance GetInstance(GraphType type, string id)
		{
			// First check to see if this is a new instance that has been cached by the transaction
			GraphInstance instance;
			if (newInstances != null && newInstances.TryGetValue(type.Name + "|" + id, out instance))
				return instance;

			// Otherwise, assume it is an existing instance
			return type.Create(id);
		}

		/// <summary>
		/// Performs all of the graph events associated with the current transaction.
		/// </summary>
		public void Perform()
		{
			GraphEventScope.Perform(() =>
			{
				int eventCount = events.Count;
				for (int i = 0; i < eventCount; i++)
				{
					GraphEvent graphEvent = events[i];
					((ITransactedGraphEvent)graphEvent).Perform(this);
					if (graphEvent is GraphInitEvent.InitNew)
						RegisterNewInstance(graphEvent.Instance);
				}
			});
		}

		/// <summary>
		/// Condenses adjacent change events that do not affect the final state of the overall transaction.
		/// </summary>
		public void Condense()
		{
			// Merge events
			int i = events.Count - 1;
			while (i > 0)
			{
				// Remove mergable events
				if (events[i - 1].Merge(events[i]))
					events.RemoveAt(i);

				// Decrement
				i--;

				// Remove invalid events
				if (!events[i].IsValid)
					events.RemoveAt(i--);
			}
		}

		/// <summary>
		/// Allows multiple <see cref="GraphTransaction"/> instances to be applied in sequence, or "chained",
		/// by propogating information about newly created instances from one transaction to the next.
		/// </summary>
		public GraphTransaction Chain(GraphTransaction nextTransaction)
		{
			// Propogate the new instance cache forward to the next transaction
			if (newInstances != null)
			{
				foreach (GraphInstance instance in newInstances.Values)
					nextTransaction.RegisterNewInstance(instance);
			}

			// Link the next transaction to the previous transaction
			nextTransaction.PreviousTransaction = this;

			// Return the next transaction
			return nextTransaction;
		}

		/// <summary>
		/// Begins the current transaction, which will record changes occurring within the scope of work.
		/// </summary>
		/// <returns>The current transaction</returns>
		/// <remarks>
		/// Always use <see cref="Record"/> when recording changes that can be represented in a single block.
		/// <see cref="Begin"/> registers for context-level events and can leak memory if <see cref="Commit"/>
		/// or <see cref="Rollback"/> are not subsequently called.
		/// <see cref="Record"/> does not attempt to roll back changes if an error occurs.
		/// </remarks>
		public GraphTransaction Begin()
		{
			if (IsActive)
				throw new InvalidOperationException("Cannot begin a transaction that is already active.");

			IsActive = true;
			Context.Event += context_Event;

			return this;
		}

		/// <summary>
		/// Commits the current transaction.
		/// </summary>
		public void Commit()
		{
			IsActive = false;
			Context.Event -= context_Event;
		}

		/// <summary>
		/// Rolls back the current transaction by calling <see cref="GraphEvent.Revert"/>
		/// in reverse order on all graph events that occurred during the transaction.
		/// </summary>
		public void Rollback()
		{
			IsActive = false;
			Context.Event -= context_Event;
			GraphEventScope.Perform(() =>
			{
				for (int i = events.Count - 1; i >= 0; i--)
					((ITransactedGraphEvent)events[i]).Rollback(this);
			});
		}


		/// <summary>
		/// Pauses the transaction and executes the action so that its changes are not recorded to the current transaction.
		/// </summary>
		/// <returns></returns>
		public GraphTransaction Exclude(Action operation)
		{
			if (!IsActive)
				throw new InvalidOperationException("Cannot pause a transaction that is inactive.");

			IsActive = false;
			Context.Event -= context_Event;

			try
			{
				operation();
			}
			catch
			{
				IsActive = true;
				Context.Event += context_Event;
				throw;
			}
			Begin();
			return this;
		}

		/// <summary>
		/// Activates a committed transaction, allowing additional changes to be recorded and appended to the current transaction.
		/// </summary>
		/// <returns></returns>
		public GraphTransaction Record(Action operation)
		{
			Begin();
			try
			{
				operation();
			}
			catch
			{
				IsActive = false;
				Context.Event -= context_Event;
				throw;
			}
			Commit();
			return this;
		}

		/// <summary>
		/// Performs a set of previous changes, performs the specified operation, and records new changes that
		/// occur as a result of the previous changes.
		/// </summary>
		/// <param name="operation"></param>
		/// <returns></returns>
		public GraphTransaction Perform(Action operation)
		{
			// Create an event scope to track changes that occur as a result of applying previous changes
			GraphEventScope eventScope = new GraphEventScope();
			try
			{
				// Perform previous changes
				Perform();

				// Return the new changes that occurred while applying the previous changes
				return Chain(new GraphTransaction()).Record(() =>
				{
					// Allow graph subscribers to be notified of the previous changes
					eventScope.Exit();

					// Clear the reference to the event scope to ensure it is not disposed twice
					eventScope = null;

					// Perform the specified operation
					if (operation != null)
						operation();
				});
			}
			catch (Exception actionException)
			{
				try
				{
					if (eventScope != null)
						eventScope.Exit();
				}
				catch (Exception disposalException)
				{
					throw new GraphEventScope.ScopeException(disposalException, actionException);
				}
				throw;
			}
		}

		#region IEnumerable<GraphEvent> Members

		IEnumerator<GraphEvent> IEnumerable<GraphEvent>.GetEnumerator()
		{
			return events.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<GraphEvent>)this).GetEnumerator();
		}

		#endregion

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			if (IsActive)
				Rollback();
		}

		#endregion

		/// <summary>
		/// Implicitly converts a array of <see cref="GraphEvent"/> instances into a <see cref="GraphTransaction"/>.
		/// </summary>
		/// <param name="events"></param>
		/// <returns></returns>
		public static implicit operator GraphTransaction(List<GraphEvent> events)
		{
			// Return null if there are no events
			if (events == null)
				return null;

			// Create a new inactive transaction
			var transaction = new GraphTransaction();

			// Initialize the events
			transaction.events = events;

			// Return the new transaction
			return transaction;
		}

		/// <summary>
		/// Combines two <see cref="GraphTransaction"/> instances into a single sequential <see cref="GraphTransaction"/>.
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		/// <returns></returns>
		public static GraphTransaction operator +(GraphTransaction first, GraphTransaction second)
		{
			return Combine(new GraphTransaction[] { first, second });
		}

		/// <summary>
		/// Combines two or more transactions creating a new 
		/// </summary>
		/// <param name="transactions"></param>
		/// <returns></returns>
		public static GraphTransaction Combine(IEnumerable<GraphTransaction> transactions)
		{
			// Get the first transaction
			GraphTransaction first = transactions.FirstOrDefault();
			if (first == null)
				return null;

			// Verify that all transactions are tied to the same context
			if (transactions.Skip(1).Any(t => t.Context != first.Context))
				throw new InvalidOperationException("Cannot combine GraphTransactions that are associated with different GraphContexts");

			// Verify than none of the transactions are still active
			if (transactions.Any(t => t.IsActive))
				throw new InvalidOperationException("Cannot combine GraphTransactions that are still active");

			// Create a new transaction and combine the information from the specified transactions
			// Return the combined transactions
			var newTransaction = new GraphTransaction();
			foreach (var transaction in transactions)
			{
				// Copy new instances
				if (transaction.newInstances != null)
				{
					if (newTransaction.newInstances == null)
						newTransaction.newInstances = new Dictionary<string, GraphInstance>();

					foreach (var entry in transaction.newInstances)
						newTransaction.newInstances.Add(entry.Key, entry.Value);
				}

				// Copy events
				newTransaction.events.AddRange(transaction.events);
			}
			return newTransaction;
		}
	}
}
