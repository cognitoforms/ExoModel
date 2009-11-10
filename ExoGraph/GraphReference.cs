using System.Collections.Generic;

namespace ExoGraph
{
	/// <summary>
	/// Represents the association between two graph instances.
	/// </summary>
	public class GraphReference : IEnumerable<GraphReference>
	{
		#region Fields

		GraphReferenceProperty property;
		GraphInstance @in;
		GraphInstance @out;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="GraphReference"/> linking two instances through the specified property.
		/// </summary>
		/// <param name="property"></param>
		/// <param name="in"></param>
		/// <param name="out"></param>
		internal GraphReference(GraphReferenceProperty property, GraphInstance @in, GraphInstance @out)
		{
			this.property = property;
			this.@in = @in;
			this.@out = @out;
		}

		#endregion

		#region Properties

		public GraphReferenceProperty Property
		{
			get
			{
				return property;
			}
		}

		public GraphInstance In
		{
			get
			{
				return @in;
			}
		}

		public GraphInstance Out
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

		IEnumerator<GraphReference> IEnumerable<GraphReference>.GetEnumerator()
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
