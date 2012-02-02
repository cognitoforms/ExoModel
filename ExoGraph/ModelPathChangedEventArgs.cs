using System;

namespace ExoModel
{
	/// <summary>
	/// Represents a change the a property along a path in the model.
	/// </summary>
	public class ModelPathChangeEvent : ModelEvent
	{
		ModelPath path;

		internal ModelPathChangeEvent(ModelInstance instance, ModelPath path)
			: base(instance)
		{
			this.path = path;
		}

		public ModelPath Path
		{
			get
			{
				return path;
			}
		}

		protected override void OnNotify()
		{
			throw new NotSupportedException("Path change events do not broadcast globally.");
		}
	}
}
