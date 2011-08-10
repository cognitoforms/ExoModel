using System;
using System.Linq;
using System.Collections.Generic;

namespace ExoGraph
{
	public class GraphStep
	{
		#region Fields

		GraphStepList nextSteps;
		GraphProperty property;

		#endregion

		#region Constructors

		internal GraphStep(GraphPath path)
		{
			this.Path = path;
			this.NextSteps = new GraphStepList();
		}

		#endregion

		#region Properties

		public GraphPath Path { get; private set; }

		public GraphStep PreviousStep { get; internal set; }

		public GraphProperty Property { get; internal set; }

		public GraphType Filter { get; internal set; }

		public GraphStepList NextSteps { get; private set; }

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
			// Exit immediately if the instance is not valid for the current step filter
			if (Filter != null && !Filter.IsInstanceOfType(instance))
				return;

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
		/// Enumerates over the set of instances represented by the current step.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public IEnumerable<GraphInstance> GetInstances(GraphInstance instance)
		{
			// Stop loading if the step is null or represents a value
			if (Property is GraphValueProperty || !((GraphReferenceProperty)Property).DeclaringType.IsInstanceOfType(instance))
				yield break;

			// Cast the property to the correct type
			var reference = (GraphReferenceProperty)Property;

			// Return each instance exposed by a list property
			if (reference.IsList)
			{
				GraphInstanceList children = instance.GetList(reference);
				if (children != null)
				{
					foreach (GraphInstance child in instance.GetList(reference))
					{
						if (Filter == null || Filter.IsInstanceOfType(child))
							yield return child;
					}
				}
			}

			// Return the instance exposed by a reference property
			else
			{
				GraphInstance child = instance.GetReference(reference);
				if (child != null && (Filter == null || Filter.IsInstanceOfType(child)))
					yield return child;
			}
		}

		/// <summary>
		/// Returns the name of the property and all child steps.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			if (Property == null)
				return "?";
			else if (NextSteps == null || NextSteps.Count == 0)
				return Property.Name + "<" + (Filter == null ? Property.DeclaringType.Name : Filter.Name) + ">";
			else if (NextSteps.Count == 1)
				return Property.Name + "<" + (Filter == null ? Property.DeclaringType.Name : Filter.Name) + ">" + "." + NextSteps.First();
			else
				return Property.Name + "<" + (Filter == null ? Property.DeclaringType.Name : Filter.Name) + ">" + "{" + NextSteps.Aggregate("", (p, s) => p.Length > 0 ? p + "," + s : s.ToString()) + "}";
		}

		#endregion
	}
}
