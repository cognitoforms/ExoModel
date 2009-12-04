using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ExoGraph
{
	/// <summary>
	/// Base class for classes that represent specific events with an object graph.
	/// </summary>
	[DataContract]
	[KnownType(typeof(GraphInitEvent))]
	[KnownType(typeof(GraphInitEvent.InitNew))]
	[KnownType(typeof(GraphInitEvent.InitExisting))]
	[KnownType(typeof(GraphDeleteEvent))]
	[KnownType(typeof(GraphPropertyGetEvent))]
	[KnownType(typeof(GraphValueChangeEvent))]
	[KnownType(typeof(GraphReferenceChangeEvent))]
	[KnownType(typeof(GraphListChangeEvent))]
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
		[DataMember(Order = 1)]
		public GraphInstance Instance
		{
			get
			{
				return instance;
			}
			internal set
			{
				instance = value;
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
	}
}
