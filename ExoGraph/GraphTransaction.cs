using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Xml;
using System.Runtime.Serialization;

namespace ExoGraph
{
	/// <summary>
	/// Tracks all <see cref="GraphEvent"/> occurrences within a context and allows changes
	/// to be recorded or rolled back entirely.
	/// </summary>
	[XmlSchemaProvider("GetSchema")]
	[DataContract]
	public class GraphTransaction : IDisposable, IEnumerable<GraphEvent>//, IXmlSerializable
	{
		GraphContext context;
		GraphFilter filter;

		[DataMember(Name = "Changes")]
		List<GraphEvent> events = new List<GraphEvent>();

		bool isActive = true;

		internal GraphTransaction(GraphContext context, GraphFilter filter)
		{
			this.context = context;
			this.filter = filter;
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
				events.Add(e);
		}

		/// <summary>
		/// Commits the current transaction.
		/// </summary>
		public void Commit()
		{
			isActive = false;
			context.Event -= context_Event;
			using (new GraphEventScope())
			{
				for (int i = events.Count - 1; i >= 0; i--)
					((ITransactedGraphEvent)events[i]).Commit();
			}
		}

		/// <summary>
		/// Rolls back the current transaction by calling <see cref="GraphEvent.Revert"/>
		/// in reverse order on all graph events that occurred during the transaction.
		/// </summary>
		public void Rollback()
		{
			isActive = false;
			context.Event -= context_Event;
			using (new GraphEventScope())
			{
				for (int i = events.Count - 1; i >= 0; i--)
					((ITransactedGraphEvent)events[i]).Rollback();
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
			if (filter != null)
			{
				foreach (GraphEvent graphEvent in events)
				{
					if (filter.Contains(graphEvent.Instance))
						yield return graphEvent;
				}
			}
			else
				foreach (GraphEvent graphEvent in events)
					yield return graphEvent;
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
