using System;
using System.Collections.Generic;

namespace ExoGraph
{
	public class GraphStep : IDisposable
	{
		#region Fields

		GraphPath path;
		GraphStep previousStep;
		IList<GraphStep> nextSteps;
		GraphProperty property;

		#endregion

		#region Constructors

		internal GraphStep(GraphPath path)
		{
			this.path = path;
		}

		#endregion

		#region Properties

		public GraphPath Path
		{
			get
			{
				return path;
			}
		}

		public GraphStep PreviousStep
		{
			get
			{
				return previousStep;
			}
			internal set
			{
				previousStep = value;
			}
		}

		public GraphProperty Property
		{
			get
			{
				return property;
			}
			internal set
			{
				property = value;
				property.Observers.Add(this);
			}
		}

		public IList<GraphStep> NextSteps
		{
			get
			{
				return nextSteps;
			}
			internal set
			{
				nextSteps = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Adds a step to the list of next steps.
		/// </summary>
		/// <param name="step">The step to add</param>
		internal void AddNextStep(GraphStep step)
		{
			nextSteps.Add(step);
		}

		/// <summary>
		/// Recursively walks up the path the current step is a member of until the
		/// root is reached and then initiates path change notification events.
		/// </summary>
		/// <param name="instance"></param>
		internal void Notify(GraphInstance instance)
		{
			// Keep walking if there are more steps
			if (PreviousStep != null)
			{
				foreach (GraphReference parentReference in instance.GetInReferences((GraphReferenceProperty)PreviousStep.Property))
					PreviousStep.Notify(parentReference.In);
			}
			// Otherwise, notify the path with the first instance instance
			else
				Path.Notify(instance);
		}

		/// <summary>
		/// Returns the name of the property the step represents.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return property.Name;
		}

		#endregion

		#region IDisposable Members

		/// <summary>
		/// Unsubscribe to graph changes.
		/// </summary>
		public void Dispose()
		{
			Property.Observers.Remove(this);
		}

		#endregion
	}
}
