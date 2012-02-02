using System;

namespace ExoModel
{
	/// <summary>
	/// Base class for classes that represent specific events with an object model.
	/// </summary>
	public abstract class ModelEvent : EventArgs
	{
		string id;
		ModelInstance instance;

		/// <summary>
		/// Creates a new <see cref="ModelEvent"/> for the specified <see cref="ModelInstance"/>.
		/// </summary>
		/// <param name="instance">The instance the event is for</param>
		internal ModelEvent(ModelInstance instance)
		{
			this.Instance = instance;
		}

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> the event is for.
		/// </summary>
		public ModelInstance Instance
		{
			get
			{
				return instance;
			}
			internal set
			{
				instance = value;
				id = instance.Id;
			}
		}

		/// <summary>
		/// Gets the id of the instance at the moment the event occurred, which may be different than the current id of the instance.
		/// </summary>
		public string InstanceId
		{
			get
			{
				return id;
			}
		}

		/// <summary>
		/// Starts a new <see cref="ModelEventScope"/>, allows subclasses to perform
		/// event specific notifications by overriding <see cref="OnNotify"/>, and
		/// notifies the context that the event has occurred.
		/// </summary>
		internal void Notify()
		{
			ModelEventScope.Perform(this, () =>
			{
				instance.Type.Context.Notify(this);
				OnNotify();
			});
		}

		/// <summary>
		/// Allows subclasses to perform event specific notification logic.
		/// </summary>
		protected abstract void OnNotify();

		/// <summary>
		/// Verifies that the specified <see cref="ModelInstance"/> refers to a valid real instance
		/// and if not, uses the type and id information to look up the real instance.
		/// </summary>
		/// <param name="transaction"></param>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected ModelInstance EnsureInstance(ModelTransaction transaction, ModelInstance instance)
		{
			return instance.Instance == null ? transaction.GetInstance(instance.Type, instance.Id) : instance;
		}

		/// <summary>
		/// Attempts to merge two model events into a single event.
		/// </summary>
		/// <param name="e"></param>
		/// <returns></returns>
		internal bool Merge(ModelEvent e)
		{
			return Instance == e.Instance && GetType() == e.GetType() && OnMerge(e);
		}

		protected virtual bool OnMerge(ModelEvent e)
		{
			return false;
		}

		/// <summary>
		/// Indicates whether the current event is valid and represents a real change to the model.
		/// </summary>
		internal virtual bool IsValid
		{
			get
			{
				return true;
			}
		}
	}
}
