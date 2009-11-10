using System.Collections.Generic;

namespace ExoGraph
{
	/// <summary>
	/// Represents a property on a type in a graph hierarchy.
	/// </summary>
	public abstract class GraphProperty
	{
		#region Fields

		string name;
		int index;
		GraphType declaringType;
		List<GraphStep> observers = new List<GraphStep>();

		#endregion

		#region Constructors

		internal GraphProperty(GraphType declaringType, string name, int index)
		{
			this.declaringType = declaringType;
			this.name = name;
			this.index = index;
		}

		#endregion

		#region Properties

		public string Name
		{
			get
			{
				return name;
			}
		}

		public int Index
		{
			get
			{
				return index;
			}
		}

		public GraphType DeclaringType
		{
			get
			{
				return declaringType;
			}
		}

		internal List<GraphStep> Observers
		{
			get
			{
				return observers;
			}
		}

		#endregion

		#region Methods

		public void OnChange(GraphInstance instance)
		{
			// Attempt to walk up the path to the root for each observer
			foreach (GraphStep observer in observers)
				observer.Notify(instance);
		}

		public override string ToString()
		{
			return name;
		}

		#endregion
	}
}
