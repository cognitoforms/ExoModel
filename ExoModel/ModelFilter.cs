using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ExoModel
{
	/// <summary>
	/// Represents a filter on a model of objects based on a specified root and 
	/// one or more predicate paths.
	/// </summary>
	/// <typeparam name="TRoot"></typeparam>
	public class ModelFilter : IDisposable, IEnumerable<ModelInstance>
	{
		#region Fields

		ModelInstance root;
		Dictionary<ModelPath, HashSet<ModelInstance>> pathModels = new Dictionary<ModelPath, HashSet<ModelInstance>>();
		HashSet<ModelInstance> model = new HashSet<ModelInstance>();
		HashSet<ModelPath> pendingChanges = new HashSet<ModelPath>();

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new model filter for the specified root object.
		/// </summary>
		/// <param name="root"></param>
		/// <param name="paths"></param>
		public ModelFilter(ModelInstance root, string[] paths)
		{
			// Store the model root
			this.root = root;

			// Delay model notifications while loading the model
			ModelEventScope.Perform(() =>
			{
				// Subscribe to paths for each predicate
				ModelType rootType = root.Type;
				foreach (string p in paths)
				{
					// Get the path for this predicate
					ModelPath path = rootType.GetPath(p);

					// Skip this predicate if it is already being monitored
					if (pathModels.ContainsKey(path))
						continue;

					// Subscribe to path change events
					path.Change += path_PathChanged;

					// Get the model for this path and add the path and model to the list of paths
					pathModels.Add(path, path.GetInstances(root));
				}
			});

			// Combine the models for each path into a universal model
			CombinePathModels();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the root the model filter is based on.
		/// </summary>
		public ModelInstance Root
		{
			get
			{
				return root;
			}
		}

		#endregion

		#region Events

		/// <summary>
		/// Notifies subscribers that the underlying model has changed.
		/// </summary>
		public event EventHandler<ModelFilterChangedEventArgs> Changed;

		#endregion

		#region Methods

		/// <summary>
		/// Indicate if the specified <see cref="ModelInstance"/> is in the current model.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public bool Contains(ModelInstance instance)
		{
			return model.Contains(instance);
		}

		/// <summary>
		/// Updates the model based on changes to a specific path.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void path_PathChanged(object sender, ModelPathChangeEvent e)
		{
			// Exit immediately if the path change occurred on a different root
			if (e.Instance != root)
				return;

			// Exit immediately if change notifications are already pending
			if (pendingChanges.Contains(e.Path))
				return;

			// Add the path to the list of pending changes
			pendingChanges.Add(e.Path);

			//Defer change notifications due to model changes until they are complete
			if (ModelEventScope.Current != null)
			{
				// Only subscribe to the first change
				if (pendingChanges.Count == 1)
					ModelEventScope.Current.Exited += (s, args) => { RaiseOnChanged(); };
			}

			// Otherwise, immediately raise the change event
			else
				RaiseOnChanged();
		}

		/// <summary>
		/// Updates the model based on changes to paths and raises the <see cref="Changed"/> event.
		/// </summary>
		void RaiseOnChanged()
		{
			// Update the model for the paths that changed
			foreach (ModelPath path in pendingChanges)
				pathModels[path] = path.GetInstances(root);
			pendingChanges.Clear();

			// Recombine all of the path models
			CombinePathModels();

			// Raise the changed event for the model filter
			if (Changed != null)
				Changed(this, new ModelFilterChangedEventArgs(this));
		}

		/// <summary>
		/// Combines all of the models for the paths the filter is monitoring.
		/// </summary>
		void CombinePathModels()
		{
			if (pathModels.Count == 1)
				this.model = pathModels.Values.First();
			else
				this.model = pathModels.Values.Aggregate(new HashSet<ModelInstance>(), (combined, path) => { combined.UnionWith(path); return combined; });
		}

		#endregion

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			foreach (ModelPath path in pathModels.Keys)
				path.Change -= path_PathChanged;
		}

		#endregion

		#region IEnumerable<ModelInstance> Members

		IEnumerator<ModelInstance> IEnumerable<ModelInstance>.GetEnumerator()
		{
			return model.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return model.GetEnumerator();
		}

		#endregion

	}
}
