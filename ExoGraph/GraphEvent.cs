using System;
using System.Collections.Generic;

namespace ExoGraph
{
	/// <summary>
	/// Base class for classes that represent specific events with an object graph.
	/// </summary>
	public abstract class GraphEvent : EventArgs
	{
		/// <summary>
		/// Stores the <see cref="GraphInstance"/> the event is for.
		/// </summary>
		GraphInstance instance;

		/// <summary>
		/// Creates a new <see cref="GraphEvent"/> for the specified <see cref="GraphInstance"/>.
		/// </summary>
		/// <param name="instance">The instance the event is for</param>
		internal GraphEvent(GraphInstance instance)
		{
			this.instance = instance;
		}

		/// <summary>
		/// Gets the <see cref="GraphInstance"/> the event is for.
		/// </summary>
		public GraphInstance Instance
		{
			get
			{
				return instance;
			}
		}

		/// <summary>
		/// Starts a new <see cref="GraphEventScope"/>, allows subclasses to perform
		/// event specific notifications by overriding <see cref="OnNotify"/>, and
		/// notifies the context that the event has occurred.
		/// </summary>
		internal void Notify()
		{
			using (new GraphEventScope(this))
			{
				OnNotify();
				instance.Type.Context.Notify(this);
			}
		}

		/// <summary>
		/// Allows subclasses to perform event specific notification logic.
		/// </summary>
		protected abstract void OnNotify();

		/// <summary>
		/// Allows subclasses to reverse the changes that caused the event to occur.
		/// </summary>
		public abstract void Revert();
	}
}
