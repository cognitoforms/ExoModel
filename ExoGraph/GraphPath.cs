using System;
using System.Linq;
using System.Collections.Generic;

namespace ExoGraph
{
	/// <summary>
	/// Represents a property path from a root <see cref="GraphType"/> with one or more steps.
	/// </summary>
	/// <remarks>
	/// Due to inheritance, property paths may branch as properties with similar names appear
	/// on siblings in the inheritance hierarchy.
	/// </remarks>
	public class GraphPath : IDisposable
	{
		#region Properties

		/// <summary>
		/// The string path the current instance represents.
		/// </summary>
		public string Path { get; private set; }

		/// <summary>
		/// The root <see cref="GraphType"/> the path starts from.
		/// </summary>
		public GraphType RootType { get; private set; }

		/// <summary>
		/// The first <see cref="Step"/> along the path.
		/// </summary>
		public GraphStep FirstStep { get; private set; }

		/// <summary>
		/// Event that is raised when any property along the path is changed.
		/// </summary>
		public event EventHandler<GraphPathChangeEvent> Change;

		#endregion

		#region Methods

		/// <summary>
		/// Creates a new <see cref="GraphPath"/> instance for the specified root <see cref="GraphType"/>
		/// and path string.
		/// </summary>
		/// <param name="rootType"></param>
		/// <param name="path"></param>
		internal static GraphPath CreatePath(GraphType rootType, string path)
		{
			// Create the new path
			GraphPath newPath = new GraphPath();

			// Recursively build the step hierarchy for the path
			List<GraphStep> steps = GetSteps(newPath, rootType, new List<string>(path.Split('.')), 0);

			// Return null if the path was not valid
			if (steps == null)
				return null;

			// Initialize and return the new path
			newPath.Path = path;
			newPath.RootType = rootType;
			newPath.FirstStep = steps[0];
			return newPath;
		}

		/// <summary>
		/// Recursively builds the steps along a property path.
		/// </summary>
		/// <param name="path"></param>
		/// <param name="graphType"></param>
		/// <param name="properties"></param>
		/// <param name="depth"></param>
		/// <returns></returns>
		static List<GraphStep> GetSteps(GraphPath path, GraphType graphType, List<string> properties, int depth)
		{
			// Create a list of steps to return
			List<GraphStep> steps = new List<GraphStep>();

			// Get the first property in the path
			GraphProperty property = graphType.Properties[properties[0]];
			
			// Paths are not supported for static properties
			if (property != null && property.IsStatic)
				return null;

			// Determine if the property is a reference on the instance type
			if (property is GraphReferenceProperty)
			{
				GraphStep step = new GraphStep(path);
				step.Property = property;

				// Find child steps if this is not the end of the path
				if (properties.Count > 1)
				{
					// Remove the first property from the beginning of the list
					properties.RemoveAt(0);

					// Recursively get the next steps for the current step
					step.NextSteps = GetSteps(path, ((GraphReferenceProperty)property).PropertyType, properties, depth);
					
					// Return null if the child steps could not be found
					if (step.NextSteps == null)
						return null;

					if (step.NextSteps.Count > 0)
					{
						foreach (GraphStep childStep in step.NextSteps)
							childStep.PreviousStep = step;
						steps.Add(step);
					}

					// Add the first property back to the beginning of the list
					properties.Insert(0, property.Name);
				}

				// Otherwise this is an reference at the end of the path
				else
				{
					step.NextSteps = new List<GraphStep>();
					steps.Add(step);
				}
			}

			// Determine if the property is a value on the instance type
			else if (property is GraphValueProperty)
			{
				GraphStep step = new GraphStep(path);
				step.Property = property;
				step.NextSteps = new List<GraphStep>();
				steps.Add(step);
			}

			// Determine if the property is a value or reference on child types
			else
			{
				// Recursively process child instance types
				foreach (GraphType childGraphType in graphType.SubTypes)
					steps.AddRange(GetSteps(path, childGraphType, properties, depth + 1));

				// If steps were not found during recursion, return null
				if (steps.Count == 0 && depth == 0)
					return null;
			}

			// Return the steps
			return steps;
		}

		/// <summary>
		/// Notify path subscribers that the path has changed.
		/// </summary>
		/// <param name="instance"></param>
		internal void Notify(GraphInstance instance)
		{
			if (Change != null)
				Change(this, new GraphPathChangeEvent(instance, this));
		}

		/// <summary>
		/// Gets the graph for the specified root object including all objects on the path.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public ICollection<GraphInstance> GetGraph(GraphInstance root)
		{
			HashSet<GraphInstance> graph = new HashSet<GraphInstance>();
			graph.Add(root);
			AddToGraph(root, FirstStep, graph);
			return graph;
		}

		/// <summary>
		/// Recursively walks path steps to add instances to the graph.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="step"></param>
		/// <param name="graph"></param>
		static void AddToGraph(GraphInstance instance, GraphStep step, HashSet<GraphInstance> graph)
		{
			// Exit immediately if the current step is a value
			if (step.Property is GraphValueProperty)
				return;

			// Recursively process child references
			foreach (GraphReference reference in instance.GetOutReferences((GraphReferenceProperty)step.Property))
			{
				// Add the instance to the graph if it has not already been added
				if (!graph.Contains(reference.Out))
					graph.Add(reference.Out);

				// Process next steps down the path
				foreach (GraphStep nextStep in step.NextSteps)
					AddToGraph(reference.Out, nextStep, graph);
			}
		}

		/// <summary>
		/// Gets all siblings of the specified value instance based on the hierarchy represented by the path.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <remarks>
		/// For example, assuming a path defined as A.B.C, an instance of C would be passed in
		/// and a list of C instances would be returned that share a common A root along a direct
		/// path from A to B to C.
		/// </remarks>
		internal IDictionary<GraphInstance, GraphInstance> GetSiblings(GraphInstance value)
		{
			// Create a dictionary of siblings
			IDictionary<GraphInstance, GraphInstance> siblings = new Dictionary<GraphInstance, GraphInstance>();

			// Load the list of roots for the value instance (likely 0 to 1)
			List<GraphInstance> roots = new List<GraphInstance>();
			List<GraphStep> lastSteps = new List<GraphStep>();
			LoadLastSteps(FirstStep, lastSteps);
			foreach (GraphStep lastStep in lastSteps)
				LoadRoots(value, lastStep, roots);

			// Populate the dictionary with siblings
			foreach (GraphInstance root in roots)
				AddToSiblings(root, FirstStep, siblings);

			// Return the dictionary of siblings
			return siblings;
		}

		/// <summary>
		/// Recursively loads the last steps for the current path.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="step"></param>
		/// <param name="roots"></param>
		static void LoadLastSteps(GraphStep step, IList<GraphStep> lastSteps)
		{
			if (step.NextSteps.Count == 0)
				lastSteps.Add(step);
			else
			{
				foreach (GraphStep nextStep in step.NextSteps)
					LoadLastSteps(nextStep, lastSteps);
			}
		}

		/// <summary>
		/// Recursively loads the roots starting with the last step of the current path.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="step"></param>
		/// <param name="roots"></param>
		static void LoadRoots(GraphInstance instance, GraphStep step, IList<GraphInstance> roots)
		{
			foreach (GraphReference reference in instance.GetInReferences((GraphReferenceProperty)step.Property))
			{
				if (step.PreviousStep == null)
					roots.Add(reference.In);
				else
					LoadRoots(reference.In, step.PreviousStep, roots);
			}
		}

		/// <summary>
		/// Recursively walks path steps to add instances to the graph.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="step"></param>
		/// <param name="graph"></param>
		static void AddToSiblings(GraphInstance instance, GraphStep step, IDictionary<GraphInstance, GraphInstance> siblings)
		{
			// Exit immediately if the current step is a value
			if (step.Property is GraphValueProperty)
				return;

			// Recursively process child references
			foreach (GraphReference reference in instance.GetOutReferences((GraphReferenceProperty)step.Property))
			{
				// Add the instance to the graph if it has not already been added and this is the last step
				if (step.NextSteps.Count == 0)
				{
					if (!siblings.ContainsKey(reference.Out))
						siblings.Add(reference.Out, reference.Out);
				}
				else
				{
					// Process next steps down the path
					foreach (GraphStep nextStep in step.NextSteps)
						AddToSiblings(reference.Out, nextStep, siblings);
				}
			}
		}

		/// <summary>
		/// Returns the string representation of the path.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Path;
		}

		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			FirstStep.Dispose();
		}

		void DisposeStep(GraphStep step)
		{
			if (step.NextSteps != null)
			{
				foreach (GraphStep nextStep in step.NextSteps)
					DisposeStep(nextStep);
			}
			step.Dispose();
		}

		#endregion
	}
}
