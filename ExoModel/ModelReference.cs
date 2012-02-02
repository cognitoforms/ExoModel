using System.Collections.Generic;
using System;

namespace ExoModel
{
	/// <summary>
	/// Represents the association between two model instances.
	/// </summary>
	[Serializable]
	public class ModelReference : IEnumerable<ModelReference>
	{
		#region Fields

		ModelReferenceProperty property;
		ModelInstance @in;
		ModelInstance @out;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="ModelReference"/> linking two instances through the specified property.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="in"></param>
		/// <param name="out"></param>
		internal ModelReference(ModelReferenceProperty property, ModelInstance @in, ModelInstance @out)
		{
			this.property = property;
			this.@in = @in;
			this.@out = @out;
		}

		#endregion

		#region Properties

		public ModelReferenceProperty Property
		{
			get
			{
				return property;
			}
		}

		public ModelInstance In
		{
			get
			{
				return @in;
			}
		}

		public ModelInstance Out
		{
			get
			{
				return @out;
			}
		}

		#endregion

		#region Methods

		internal void Destroy()
		{
			// Remove reference from parent and child instances
			@in.RemoveReference(this);

			// Clear field references
			this.property = null;
			this.@in = null;
			this.@out = null;
		}

		public override string ToString()
		{
			return property.ToString();
		}

		IEnumerator<ModelReference> IEnumerable<ModelReference>.GetEnumerator()
		{
			yield return this;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			yield return this;
		}

		#endregion
	}
}
