using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
		Dictionary<GraphPath, HashSet<GraphInstance>> pathGraphs = new Dictionary<GraphPath, HashSet<GraphInstance>>();
		HashSet<GraphInstance> graph = new HashSet<GraphInstance>();
		HashSet<GraphPath> pendingChanges = new HashSet<GraphPath>();

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new graph filter for the specified root object.
		/// </summary>
		/// <param name="root"></param>
		/// <param name="paths"></param>
		public GraphFilter(GraphInstance root, string[] paths)
		{
			// Store the graph root
			this.root = root;

			// Delay graph notifications while loading the graph
			GraphEventScope.Perform(() =>
			{
				// Subscribe to paths for each predicate
				GraphType rootType = root.Type;
				foreach (string p in paths)
				{
					// Get the path for this predicate
					GraphPath path = rootType.GetPath(p);

					// Skip this predicate if it is already being monitored
					if (pathGraphs.ContainsKey(path))
						continue;

					// Subscribe to path change events
					path.Change += path_PathChanged;

					// Get the graph for this path and add the path and graph to the list of paths
					pathGraphs.Add(path, path.GetInstances(root));
				}
			});

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
				pathGraphs[path] = path.GetInstances(root);
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
			if (pathGraphs.Count == 1)
				this.graph = pathGraphs.Values.First();
			else
				this.graph = pathGraphs.Values.Aggregate(new HashSet<GraphInstance>(), (combined, path) => { combined.UnionWith(path); return combined; });
		}

		#endregion

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			foreach (GraphPath path in pathGraphs.Keys)
				path.Change -= path_PathChanged;
		}

		#endregion

		#region IEnumerable<GraphInstance> Members

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
