using System;
using System.Collections;
using System.Collections.Generic;

namespace ExoGraph
{
	/// <summary>
	/// Represents a filter on a graph of objects based on a specified root and 
	/// one or more predicate paths.
	/// </summary>
	/// <typeparam name="TRoot"></typeparam>
	public class GraphFilter : IDisposable, IEnumerable<GraphInstance>
	{
		#region Fields

		GraphInstance root;
		Dictionary<GraphPath, ICollection<GraphInstance>> pathGraphs = new Dictionary<GraphPath, ICollection<GraphInstance>>();
		ICollection<GraphInstance> graph = new HashSet<GraphInstance>();
		List<GraphPath> pendingChanges = new List<GraphPath>();

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new graph filter for the specified root object.
		/// </summary>
		/// <param name="root"></param>
		/// <param name="predicates"></param>
		public GraphFilter(GraphInstance root, params string[] predicates)
		{
			// Store the graph root
			this.root = root;

			// Delay graph notifications while loading the graph
			using (new GraphEventScope())
			{
				// Subscribe to paths for each predicate
				GraphType rootType = root.Type;
				foreach (string predicate in predicates)
				{
					// Get the path for this predicate
					GraphPath path = rootType.GetPath(predicate);

					// Skip this predicate if it is already being monitored
					if (pathGraphs.ContainsKey(path))
						continue;

					// Subscribe to path change events
					path.Change += path_PathChanged;

					// Force the graph to load along this path
					Load(root, path.FirstStep);

					// Get the graph for this path and add the path and graph to the list of paths
					pathGraphs.Add(path, path.GetGraph(root));
				}
			}

			// Combine the graphs for each path into a universal graph
			CombinePathGraphs();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the root the graph filter is based on.
		/// </summary>
		public GraphInstance Root
		{
			get
			{
				return root;
			}
		}

		#endregion

		#region Events

		/// <summary>
		/// Notifies subscribers that the underlying graph has changed.
		/// </summary>
		public event EventHandler<GraphFilterChangedEventArgs> Changed;

		#endregion

		#region Methods

		/// <summary>
		/// Indicate if the specified <see cref="GraphInstance"/> is in the current graph.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public bool Contains(GraphInstance instance)
		{
			return graph.Contains(instance);
		}

		/// <summary>
		/// Recursively loads a property path in the graph by walking steps.
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="step"></param>
		void Load(GraphInstance instance, GraphStep step)
		{
			// Stop loading if the step is null or represents a value
			if (step == null || step.Property is GraphValueProperty || !((GraphReferenceProperty)step.Property).DeclaringType.IsInstanceOfType(instance))
				return;

			// Cast the property to the correct type
			var reference = (GraphReferenceProperty)step.Property;

			// Recursively load steps for a list of business objects
			if (reference.IsList)
			{
				GraphInstanceList children = instance.GetList(reference);
				if (children != null)
				{
					foreach (GraphInstance child in instance.GetList(reference))
					{
						foreach (GraphStep childStep in step.NextSteps)
							Load(child, childStep);
					}
				}
			}

			// Recursively load steps for a business object
			else
			{
				GraphInstance child = instance.GetReference(reference);
				if (child != null)
				{
					foreach (GraphStep childStep in step.NextSteps)
						Load(child, childStep);
				}
			}
		}

		/// <summary>
		/// Updates the graph based on changes to a specific path.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void path_PathChanged(object sender, GraphPathChangeEvent e)
		{
			// Exit immediately if the path change occured on a different root
			if (e.Instance != root)
				return;

			// Exit immediately if change notifications are already pending
			if (pendingChanges.Contains(e.Path))
				return;

			// Add the path to the list of pending changes
			pendingChanges.Add(e.Path);

			//Defer change notifications due to graph changes until they are complete
			if (GraphEventScope.Current != null)
			{
				// Only subscribe to the first change
				if (pendingChanges.Count == 1)
					GraphEventScope.Current.Exited += (s, args) => { RaiseOnChanged(); };
			}

			// Otherwise, immediately raise the change event
			else
				RaiseOnChanged();
		}

		/// <summary>
		/// Updates the graph based on changes to paths and raises the <see cref="Changed"/> event.
		/// </summary>
		void RaiseOnChanged()
		{
			// Update the graph for the paths that changed
			foreach (GraphPath path in pendingChanges)
				pathGraphs[path] = path.GetGraph(root);
			pendingChanges.Clear();

			// Recombine all of the path graphs
			CombinePathGraphs();

			// Raise the changed event for the graph filter
			if (Changed != null)
				Changed(this, new GraphFilterChangedEventArgs(this));
		}

		/// <summary>
		/// Combines all of the graphs for the paths the filter is monitoring.
		/// </summary>
		void CombinePathGraphs()
		{
			ICollection<GraphInstance> graph = new HashSet<GraphInstance>();
			foreach (ICollection<GraphInstance> pathGraph in pathGraphs.Values)
			{
				foreach (GraphInstance instance in pathGraph)
					if (!graph.Contains(instance))
						graph.Add(instance);
			}
			this.graph = graph;
		}

		#endregion

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			foreach (GraphPath path in pathGraphs.Keys)
				path.Change -= path_PathChanged;
		}

		#endregion

		#region IEnumerable<Vertex> Members

		IEnumerator<GraphInstance> IEnumerable<GraphInstance>.GetEnumerator()
		{
			return graph.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return graph.GetEnumerator();
		}

		#endregion

	}
}
