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
	[DataContract]
	public class GraphTransaction : IDisposable, IEnumerable<GraphEvent>
	{
		Dictionary<string, GraphInstance> newInstances;
		GraphContext context;

		[DataMember(Name = "changes")]
		List<GraphEvent> events = new List<GraphEvent>();

		bool isActive = true;

		internal GraphTransaction(GraphContext context)
		{
			this.context = context;
			context.Event += context_Event;
		}

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
				if (e is GraphSaveEvent && newInstances != null)
				{
					GraphSaveEvent saveEvent = (GraphSaveEvent)e;
					foreach (KeyValuePair<string, GraphInstance> instance in newInstances)
					{
						string oldId = instance.Key.Substring(instance.Key.IndexOf("|") + 1);
						if (oldId != instance.Value.Id)
							saveEvent.AddIdChange(instance.Value.Type, oldId, instance.Value.Id);
					}
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
			using (new GraphEventScope())
			{
				int eventCount = events.Count;
				for (int i = 0; i < eventCount; i++)
				{
					GraphEvent graphEvent = events[i];
					((ITransactedGraphEvent)graphEvent).Perform(this);
					if (graphEvent is GraphInitEvent.InitNew)
						RegisterNewInstance(graphEvent.Instance);
				}
			}
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

				// Begin tracking new changes
				using (GraphTransaction newChanges = GraphContext.Current.BeginTransaction())
				{
					// Propogate the new instance cache forward to the new transaction
					if (newInstances != null)
					{
						foreach (GraphInstance instance in newInstances.Values)
							newChanges.RegisterNewInstance(instance);
					}

					// Allow graph subscribers to be notified of the previous changes
					((IDisposable) eventScope).Dispose();

					// Clear the reference to the event scope to ensure it is not disposed twice
					eventScope = null;

					// Perform the specified operation
					if (operation != null)
						operation();

					// Commit the transaction
					newChanges.Commit();

					// Return the new changes that occurred while applying the previous changes
					return newChanges;
				}
			} catch (Exception e)
			{
				throw e;
			}
			finally
			{
				// Make sure the event scope is disposed if an unexpected error occurs
				if (eventScope != null)
					((IDisposable)eventScope).Dispose();
			}
		}

		/// <summary>
		/// Commits the current transaction.
		/// </summary>
		public void Commit()
		{
			isActive = false;
			Context.Event -= context_Event;
			using (new GraphEventScope())
			{
				for (int i = events.Count - 1; i >= 0; i--)
					((ITransactedGraphEvent)events[i]).Commit(this);
			}
		}

		/// <summary>
		/// Rolls back the current transaction by calling <see cref="GraphEvent.Revert"/>
		/// in reverse order on all graph events that occurred during the transaction.
		/// </summary>
		public void Rollback()
		{
			isActive = false;
			Context.Event -= context_Event;
			using (new GraphEventScope())
			{
				for (int i = events.Count - 1; i >= 0; i--)
					((ITransactedGraphEvent)events[i]).Rollback(this);
			}
		}

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			if (isActive)
				Rollback();
		}

		#endregion

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

			// Create a new transaction
			var transaction = new GraphTransaction(GraphContext.Current);

			// Initialize the events
			transaction.events = (List<GraphEvent>)events;

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
			if (first.Context != second.Context)
				throw new InvalidOperationException("Cannot combine GraphTransactions that are associated with different GraphContexts");

			if (first.isActive || second.isActive)
				throw new InvalidOperationException("Cannot combine GraphTransactions that are still active");

			GraphTransaction newTransaction;

			using (newTransaction = first.Context.BeginTransaction())
			{
				if (first.newInstances != null)
				{
					if (newTransaction.newInstances == null)
						newTransaction.newInstances = new Dictionary<string, GraphInstance>();

					foreach (var entry in first.newInstances)
						newTransaction.newInstances.Add(entry.Key, entry.Value);
				}

				if (second.newInstances != null)
				{
					if (newTransaction.newInstances == null)
						newTransaction.newInstances = new Dictionary<string, GraphInstance>();

					foreach (var entry in second.newInstances)
						newTransaction.newInstances.Add(entry.Key, entry.Value);
				}

				newTransaction.events.AddRange(first.events);
				newTransaction.events.AddRange(second.events);

				newTransaction.Commit();
			}

			return newTransaction;
		}
	}
}
