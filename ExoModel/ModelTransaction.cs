using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;

namespace ExoModel
{
	/// <summary>
	/// Tracks all <see cref="ModelEvent"/> occurrences within a context and allows changes
	/// to be recorded or rolled back entirely.
	/// </summary>
	public class ModelTransaction : IEnumerable<ModelEvent>, IDisposable
	{
		Dictionary<string, ModelInstance> newInstances;
		ModelContext context;

		LinkedList<ModelEvent> events = new LinkedList<ModelEvent>();

		/// <summary>
		/// Records <see cref="ModelEvent"/> occurences within the current context.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void context_Event(object sender, ModelEvent e)
		{
			// Only track events that change the model.
			// Don't track changes to cached objects.
			if (e is ITransactedModelEvent && !e.Instance.IsCached)
			{
				// Track id mappings for new instances created during the transaction
				if (e is ModelInitEvent.InitNew)
					RegisterNewInstance(e.Instance);

				// Populate save events with ids that have changed during the transaction
				else if (e is ModelSaveEvent)
				{
					HashSet<ModelInstance> added = new HashSet<ModelInstance>();
					HashSet<ModelInstance> modified = new HashSet<ModelInstance>();
					HashSet<ModelInstance> deleted = new HashSet<ModelInstance>();

					for (var transaction = this; transaction != null; transaction = transaction.PreviousTransaction)
					{
						// Process the transaction log in reverse order, searching for instances 
						// that were persisted by the save event
						for (var node = transaction.events.Last; node != null; node = node.Previous)
						{
							var evt = node.Value;

							// Stop processing if a previous save event is encountered
							if (evt is ModelSaveEvent)
								goto UpdateSave;

							// Deleted
							if (evt.Instance.IsDeleted)
								deleted.Add(evt.Instance);

							// Added
							else if (evt is ModelInitEvent.InitNew && !evt.Instance.IsNew)
								added.Add(evt.Instance);

							// Modified
							else if (!evt.Instance.IsNew && !evt.Instance.IsModified)
								modified.Add(evt.Instance);

						}
					}

				UpdateSave: ModelSaveEvent saveEvent = (ModelSaveEvent)e;
					saveEvent.Added = added.ToArray();
					saveEvent.Deleted = deleted.ToArray();
					saveEvent.Modified = modified.Except(saveEvent.Added).Except(saveEvent.Deleted).ToArray();
				}

				// Add the transacted event to the list of events for the transaction
				events.AddLast(e);
			}
		}

		ModelContext Context
		{
			get
			{
				if (context == null)
					context = ModelContext.Current;
				return context;
			}
		}

		public bool IsActive { get; private set; }

		public ModelTransaction PreviousTransaction { get; private set; }

		private string GetNewInstanceKey(ModelType modelType, string id)
		{
			return modelType.Name + "|" + id;
		}

		void RegisterNewInstance(ModelInstance instance)
		{
			if (newInstances == null)
				newInstances = new Dictionary<string, ModelInstance>();
			newInstances[GetNewInstanceKey(instance.Type, instance.Id)] = instance;
		}

		/// <summary>
		/// Gets a <see cref="ModelInstance"/> with the specified type and id, which may be either an existing
		/// instance or a new instance created during the scope of the current transaction.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		public ModelInstance GetInstance(ModelType type, string id)
		{
			// First check to see if this is a new instance that has been cached by the transaction
			ModelInstance instance;
			if (newInstances != null && newInstances.TryGetValue(GetNewInstanceKey(type, id), out instance))
				return instance;

			// Otherwise, assume it is an existing instance
			return type.Create(id);
		}

		/// <summary>
		/// Returns true if the instance is new and contained within the transaction.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public bool ContainsNewInstance(ModelInstance instance)
		{
			if (newInstances == null || !instance.IsNew)
				return false;
			else
			{
				string key = GetNewInstanceKey(instance.Type, instance.Id);
				return newInstances.ContainsKey(key) && newInstances[key] != null;
			}
		}

		/// <summary>
		/// Performs all of the model events associated with the current transaction.
		/// </summary>
		public void Perform()
		{
			ModelEventScope.Perform(() =>
			{
				foreach (var modelEvent in events)
				{
					((ITransactedModelEvent)modelEvent).Perform(this);
					if (modelEvent is ModelInitEvent.InitNew)
						RegisterNewInstance(modelEvent.Instance);
				}
			});
		}

		/// <summary>
		/// Truncates all previous events for instances when a state change occurs
		/// </summary>
		public void Truncate()
		{
			HashSet<ModelInstance> removeEventInstances = new HashSet<ModelInstance>();

			for (var transaction = this; transaction != null; transaction = transaction.PreviousTransaction)
			{
				// Process the transaction log in reverse order, searching for changes to remove
				var node = events.Last;
				while (node != null)
				{
					var evt = node.Value;
					var previous = node.Previous;

					if (evt is ModelSaveEvent)
					{
						ModelSaveEvent saveEvent = (ModelSaveEvent)evt;
						removeEventInstances.UnionWith(saveEvent.Added);
						removeEventInstances.UnionWith(saveEvent.Deleted);
						removeEventInstances.UnionWith(saveEvent.Modified);

						transaction.events.Remove(node);
					}
					else if (removeEventInstances.Contains(evt.Instance))
						transaction.events.Remove(node);

					node = previous;
				}
			}
		}

		/// <summary>
		/// Purges all events that match the specified criteria.
		/// </summary>
		public void Purge(Func<ModelEvent, bool> criteria)
		{
			// Purge previous transactions linked to the current transaction too
			if (PreviousTransaction != null)
				PreviousTransaction.Purge(criteria);

			// Remove events that match the specified criteria
			var node = events.First;
			while (node != null)
			{
				var next = node.Next;
				if (criteria(node.Value))
					events.Remove(node);
				node = next;
			}
		}

		/// <summary>
		/// Condenses adjacent change events that do not affect the final state of the overall transaction.
		/// </summary>
		public void Condense()
		{
			var node = events.First;
			while (node != null)
			{
				// Remove invalid events
				if (!node.Value.IsValid)
				{
					var next = node.Next;
					events.Remove(node);
					node = next;
				}
				// Attempt to merge events that follow
				else
				{
					// Remove mergable events
					while (node.Next != null && node.Value.Merge(node.Next.Value))
						events.Remove(node.Next);

					node = node.Next;
				}
			}
		}

		/// <summary>
		/// Allows multiple <see cref="ModelTransaction"/> instances to be applied in sequence, or "chained",
		/// by propogating information about newly created instances from one transaction to the next.
		/// </summary>
		public ModelTransaction Chain(ModelTransaction nextTransaction)
		{
			// Immediately exit if the transaction is being chained with itself
			if (nextTransaction == this)
				return nextTransaction;

			// Propogate the new instance cache forward to the next transaction
			if (newInstances != null)
			{
				foreach (ModelInstance instance in newInstances.Values)
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
		public ModelTransaction Begin()
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
		/// Rolls back the current transaction by calling <see cref="ModelEvent.Revert"/>
		/// in reverse order on all model events that occurred during the transaction.
		/// </summary>
		public void Rollback()
		{
			IsActive = false;
			Context.Event -= context_Event;
			ModelEventScope.Perform(() =>
			{
				for (var node = events.Last; node != null; node = node.Previous)
					((ITransactedModelEvent)node.Value).Rollback(this);
			});
		}

		/// <summary>
		/// Pauses the transaction and executes the action so that its changes are not recorded to the current transaction.
		/// </summary>
		/// <returns></returns>
		public ModelTransaction Exclude(Action operation)
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
		/// Pauses the transaction and executes the action so that its changes are not recorded to the current transaction.
		/// </summary>
		/// <returns></returns>
		public ModelTransaction Exclude(Action operation, Func<ModelEvent, bool> excluded)
		{
			if (!IsActive)
				throw new InvalidOperationException("Cannot pause a transaction that is inactive.");

			Context.Event -= context_Event;

			var eventFilter = new EventFilter() { filter = (evt) => !excluded(evt), transaction = this };

			Context.Event += eventFilter.RecordEvents;

			try
			{
				operation();
			}
			finally
			{
				Context.Event -= eventFilter.RecordEvents;
				Context.Event += context_Event;
			}

			return this;
		}

		/// <summary>
		/// Activates a committed transaction, allowing additional changes to be recorded and appended to the current transaction.
		/// </summary>
		/// <returns></returns>
		public ModelTransaction Record(Action operation, int? maxKnownId = null)
		{
			Begin();

			if (maxKnownId.HasValue)
				ModelContext.Current.SetLastNonCachedInstanceId(maxKnownId.Value);

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
		/// Activates a committed transaction, allowing additional changes to be recorded and appended to the current transaction.
		/// </summary>
		/// <returns></returns>
		public ModelTransaction Record(Action operation, Func<ModelEvent, bool> filter)
		{
			if (IsActive)
				throw new InvalidOperationException("Cannot begin a transaction that is already active.");

			var eventFilter = new EventFilter() { filter = filter, transaction = this };

			IsActive = true;
			Context.Event += eventFilter.RecordEvents;

			try
			{
				operation();
			}
			finally
			{
				IsActive = false;
				Context.Event -= eventFilter.RecordEvents;
			}
			return this;
		}

		/// <summary>
		/// Supports excluding model events that do not match the given filter
		/// </summary>
		class EventFilter
		{
			internal Func<ModelEvent, bool> filter;

			internal ModelTransaction transaction;

			internal void RecordEvents(object sender, ModelEvent e)
			{
				if (filter(e))
					transaction.context_Event(sender, e);
			}
		}

		/// <summary>
		/// Performs a set of previous changes, performs the specified operation, and records new changes that
		/// occur as a result of the previous changes and the specified operation.
		/// </summary>
		public ModelTransaction Perform(Action operation, int? maxKnownId = null)
		{
			return Perform(operation, new ModelTransaction(), maxKnownId);
		}

		/// <summary>
		/// Performs a set of previous changes, performs the specified operation, and records new changes that
		/// occur as a result of the previous changes and the specified operation.
		/// </summary>
		public ModelTransaction Perform(Action operation, ModelTransaction transaction, int? maxKnownId = null)
		{
			// Create an event scope to track changes that occur as a result of applying previous changes
			ModelEventScope eventScope = new ModelEventScope();
			try
			{
				// Perform previous changes
				Perform();

				// Return the new changes that occurred while applying the previous changes
				return Chain(transaction).Record(() =>
				{
					// Allow model subscribers to be notified of the previous changes
					eventScope.Exit();

					// Clear the reference to the event scope to ensure it is not disposed twice
					eventScope = null;

					// Perform the specified operation
					if (operation != null)
						operation();
				}, maxKnownId);
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
					throw new ModelEventScope.ScopeException(disposalException, actionException);
				}
				throw;
			}
		}

		#region IEnumerable<ModelEvent> Members

		IEnumerator<ModelEvent> IEnumerable<ModelEvent>.GetEnumerator()
		{
			return events.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<ModelEvent>)this).GetEnumerator();
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
		/// Implicitly converts a array of <see cref="ModelEvent"/> instances into a <see cref="ModelTransaction"/>.
		/// </summary>
		/// <param name="events"></param>
		/// <returns></returns>
		public static implicit operator ModelTransaction(List<ModelEvent> events)
		{
			// Return null if there are no events
			if (events == null)
				return null;

			// Create a new inactive transaction
			var transaction = new ModelTransaction();

			// Initialize the events
			transaction.events = new LinkedList<ModelEvent>(events);

			// Return the new transaction
			return transaction;
		}

		/// <summary>
		/// Combines two <see cref="ModelTransaction"/> instances into a single sequential <see cref="ModelTransaction"/>.
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		/// <returns></returns>
		public static ModelTransaction operator +(ModelTransaction first, ModelTransaction second)
		{
			return Combine(new ModelTransaction[] { first, second });
		}

		/// <summary>
		/// Combines two or more transactions creating a new transaction.
		/// </summary>
		/// <param name="transactions"></param>
		/// <returns></returns>
		public static ModelTransaction Combine(IEnumerable<ModelTransaction> transactions)
		{
			// Get the first transaction
			ModelTransaction first = transactions.FirstOrDefault();
			if (first == null)
				return null;

			// Verify that all transactions are tied to the same context
			if (transactions.Skip(1).Any(t => t.Context != first.Context))
				throw new InvalidOperationException("Cannot combine ModelTransactions that are associated with different ModelContexts");

			// Verify than none of the transactions are still active
			if (transactions.Any(t => t.IsActive))
				throw new InvalidOperationException("Cannot combine ModelTransactions that are still active");

			// Create a new transaction and combine the information from the specified transactions
			// Return the combined transactions
			var newTransaction = new ModelTransaction();
			foreach (var transaction in transactions)
			{
				// Copy new instances
				if (transaction.newInstances != null)
				{
					if (newTransaction.newInstances == null)
						newTransaction.newInstances = new Dictionary<string, ModelInstance>();

					foreach (var entry in transaction.newInstances)
						newTransaction.newInstances.Add(entry.Key, entry.Value);
				}

				// Copy events
				foreach (var evt in transaction.events)
					newTransaction.events.AddLast(evt);
			}
			return newTransaction;
		}
	}
}
