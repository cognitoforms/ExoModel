﻿using System;
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

				// Populate commit events with ids that have changed during the transaction
				if (e is GraphSaveEvent && newInstances != null)
				{
					GraphSaveEvent commitEvent = (GraphSaveEvent)e;
					foreach (KeyValuePair<string, GraphInstance> instance in newInstances)
					{
						string oldId = instance.Key.Substring(instance.Key.IndexOf("|") + 1);
						if (oldId != instance.Value.Id)
							commitEvent.AddIdChange(instance.Value.Type, oldId, instance.Value.Id);
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
			object obj = Context.GetInstance(type, id);
			return obj == null ? null : Context.GetGraphInstance(obj);
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
					((IDisposable)eventScope).Dispose();

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
	}
}
