using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
		#region Fields

		EventHandler<GraphPathChangeEvent> change;

		#endregion

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
		/// The first <see cref="Step"/>s along the path.
		/// </summary>
		public GraphStepList FirstSteps { get; private set; }

		/// <summary>
		/// Event that is raised when any property along the path is changed.
		/// </summary>
		public event EventHandler<GraphPathChangeEvent> Change
		{
			add
			{
				// Subscribe to path changes if this is the first change event subscription
				if (change == null || change.GetInvocationList().Length == 0)
					Subscribe(FirstSteps);

				change += value;
			}
			remove
			{
				change -= value;

				// Unsubscribe from path changes if this was the last change event subscription
				if (change != null && change.GetInvocationList().Length == 0)
					Unsubscribe(FirstSteps);
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Recursively subscribes to path changes.
		/// </summary>
		/// <param name="steps"></param>
		static void Subscribe(GraphStepList steps)
		{
			foreach (var step in steps)
			{
				step.Property.Observers.Add(step);
				Subscribe(step.NextSteps);
			}
		}

		/// <summary>
		/// Recursively unsubscribes to path changes.
		/// </summary>
		/// <param name="steps"></param>
		static void Unsubscribe(GraphStepList steps)
		{
			foreach (var step in steps)
			{
				step.Property.Observers.Remove(step);
				Unsubscribe(step.NextSteps);
			}
		}

		/// <summary>
		/// Creates a new <see cref="GraphPath"/> instance for the specified root <see cref="GraphType"/>
		/// and path string.
		/// </summary>
		/// <param name="rootType"></param>
		/// <param name="path"></param>
		internal static GraphPath CreatePath(GraphType rootType, string path)
		{
			// Create the new path
			GraphPath newPath = new GraphPath()
			{
				RootType = rootType
			};

			// Create a temporary root step
			var root = new GraphStep(newPath);

			// Parse the instance path
			var match = pathParser.Match(path);
			if (match == null)
				throw new ArgumentException("The specified path, '" + path + "', is not valid.");

			var steps = new List<GraphStep>() { root };
			var stack = new Stack<List<GraphStep>>();
			var tokens = match.Groups[1].Captures.Cast<Capture>().Select(c => c.Value).ToArray();
			for (int i = 0; i < tokens.Length; i++)
			{
				var token = tokens[i];
				switch (token[0])
				{
					case '{':
						stack.Push(steps);
						break;
					case '}':
						steps = stack.Pop();
						break;
					case ',':
						steps = stack.Peek();
						break;
					case '.':
						break;
					case '<':
						var filter = rootType.Context.GetGraphType(token.Substring(1, token.Length - 2));
						foreach (var step in steps)
						{
							if (step.Property is GraphReferenceProperty &&
								(((GraphReferenceProperty)step.Property).PropertyType == filter ||
								((GraphReferenceProperty)step.Property).PropertyType.IsSubType(filter)))
								step.Filter = filter;
							else
								RemoveStep(step);
						}
						break;
					default:

						// Get the property name for the next step
						var propertyName = token.Trim();
						if (propertyName == "")
							continue;

						// Track the next steps
						var nextSteps = new List<GraphStep>();

						// Process each of the current steps
						foreach (var step in steps)
						{
							// Ensure the current step is a valid reference property
							if (step.Property != null && step.Property is GraphValueProperty)
								throw new ArgumentException("Property '" + step.Property.Name + "' is a value property and cannot have child properties specified.");

							// Get the type of the current step
							var currentType = step.Property != null ? ((GraphReferenceProperty)step.Property).PropertyType : step.Path.RootType;
							if (step.Filter != null)
							{
								if (step.Filter != currentType && !currentType.IsSubType(step.Filter))
									throw new ArgumentException("Filter type '" + step.Filter.Name + "' is not a valid subtype of '" + currentType.Name + "'.");
								currentType = step.Filter;
							}

							// Process the current and all subtypes, honoring any specified type filters
							foreach (var type in currentType.GetDescendentsInclusive())
							{
								// Get the current property
								var property = type.Properties[propertyName];

								// Ensure the property is valid
								if (property == null || property.IsStatic || (property.DeclaringType != type && type != currentType && currentType.Properties[propertyName] != null))
									continue;

								// Look ahead to see if this step is filtered
								filter = i < tokens.Length - 1 && tokens[i + 1].StartsWith("<") ?
									rootType.Context.GetGraphType(tokens[i + 1].Substring(1, tokens[i + 1].Length - 2)) :
									null;

								// See if the step already exists for this property and filter or needs to be created
								var nextStep = step.NextSteps.Where(s => s.Property == property && s.Filter == filter).FirstOrDefault();
								if (nextStep == null)
								{
									nextStep = new GraphStep(newPath) { Property = property, Filter = filter, PreviousStep = step.Property != null ? step : null };
									step.NextSteps.Add(nextStep);
								}
								nextSteps.Add(nextStep);
							}

							// Remove steps that do not lead to the end of the path
							RemoveStep(step);
						}

						// Immediately exit if no steps were found matching the requested path
						if (nextSteps.Count == 0)
							return null;

						steps = nextSteps;
						break;
				}
			}

			// Throw an exception if there are unmatched property group delimiters
			if (stack.Count > 0)
				throw new ArgumentException("Unclosed '{' in path: " + path, "path");

			// Return null if the path was not valid
			if (!root.NextSteps.Any())
				return null;

			// Otherwise, finish initializing and return the new path
			newPath.FirstSteps = root.NextSteps;
			newPath.Path = path;
			return newPath;
		}

		/// <summary>
		/// Safely removes an invalid step from a path.
		/// </summary>
		/// <param name="step"></param>
		static void RemoveStep(GraphStep step)
		{
			// Remove steps that do not lead to the end of the path
			if (step.Property != null && !step.NextSteps.Any())
			{
				var previousStep = step;
				while (previousStep != null && !previousStep.NextSteps.Any())
				{
					previousStep.NextSteps.Remove(previousStep);
					previousStep = previousStep.PreviousStep;
				}
			}
		}

		/// <summary>
		/// Parses query paths into tokens for processing
		/// </summary>
		static Regex pathParser = new Regex(@"^([a-zA-Z0-9_]+|[{}.,]|\s|(\<[a-zA-Z0-9_.]+\>))*$", RegexOptions.Compiled);

		/// <summary>
		/// Notify path subscribers that the path has changed.
		/// </summary>
		/// <param name="instance"></param>
		internal void Notify(GraphInstance instance)
		{
			if (change != null)
				change(this, new GraphPathChangeEvent(instance, this));
		}

		/// <summary>
		/// Gets the graph for the specified root object including all objects on the path.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public HashSet<GraphInstance> GetInstances(GraphInstance root)
		{
			var graph = new HashSet<GraphInstance>();
			GetInstances(root, FirstSteps, graph);
			return graph;
		}

		/// <summary>
		/// Recursively loads a path in the graph by walking steps.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="step"></param>
		/// <param name="graph"></param>
		void GetInstances(GraphInstance instance, GraphStepList steps, HashSet<GraphInstance> graph)
		{
			// Add the instance to the graph
			graph.Add(instance);

			// Process each child step
			foreach (var step in steps)
			{
				// Process each instance
				foreach (var child in step.GetInstances(instance))
					GetInstances(child, step.NextSteps, graph);
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
			// Unsubscribe all steps along the path
			Unsubscribe(FirstSteps);
		}

		#endregion
	}
}
