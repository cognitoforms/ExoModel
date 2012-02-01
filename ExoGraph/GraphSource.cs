using System;
using System.Linq;

namespace ExoGraph
{
	/// <summary>
	/// Utility class that supports accessing the value of a property along a static or instance source path.
	/// </summary>
	public class GraphSource
	{
		string[] sourcePath;
		
		/// <summary>
		/// Creates a new <see cref="GraphSource"/> for the specified root type and path.
		/// </summary>
		/// <param name="rootType">The root type name, which is required for instance paths</param>
		/// <param name="path">The source path, which is either an instance path or a static path</param>
		public GraphSource(GraphPath path)
		{
			InitializeFromInstancePath(path);
		}

		/// <summary>
		/// Creates a new <see cref="GraphSource"/> for the specified root type and path.
		/// </summary>
		/// <param name="rootType">The root type name, which is required for instance paths</param>
		/// <param name="path">The source path, which is either an instance path or a static path</param>
		public GraphSource(GraphType rootType, string path)
		{
			// Instance Path
			GraphPath instancePath;
			if (rootType != null && rootType.TryGetPath(path, out instancePath))
			{
				InitializeFromInstancePath(instancePath);
			}

			// Static Path
			else if (path.Contains('.'))
			{
				// Store the source path
				this.Path = path;
				this.IsStatic = true;

				var sourceGraphType = GraphContext.Current.GetGraphType(path.Substring(0, path.LastIndexOf('.')));
				if (sourceGraphType != null)
				{
					var sourceGraphProperty = sourceGraphType.Properties[path.Substring(path.LastIndexOf('.') + 1)];
					if (sourceGraphProperty != null && sourceGraphProperty.IsStatic)
					{
						this.SourceProperty = sourceGraphProperty.Name;
						this.SourceType = sourceGraphProperty.DeclaringType.Name;
					}
				}
			}

			// Raise an error if the specified path is not valid
			if (SourceProperty == null)
				throw new ArgumentException("The specified path, '" + path + "', was not valid for the root type of '" + rootType.Name + "'.", "path");
		}

		/// <summary>
		/// Initialize the source from a valid <see cref="GraphPath"/>
		/// </summary>
		/// <param name="instancePath"></param>
		private void InitializeFromInstancePath(GraphPath instancePath)
		{
			this.Path = instancePath.Path;
			this.IsStatic = false;

			this.RootType = instancePath.RootType.Name;
			this.sourcePath = instancePath.Path.Split('.');
			this.sourcePath = sourcePath.Take(sourcePath.Length - 1).ToArray();

			// Get the last property along the path
			GraphStep step = instancePath.FirstSteps.First();
			while (step.NextSteps.Any())
				step = step.NextSteps.First();
			this.SourceProperty = step.Property.Name;
			this.SourceType = step.Property.DeclaringType.Name;
		}

		/// <summary>
		/// Gets the name of the type that is the starting point for the source path.
		/// </summary>
		public string RootType { get; private set; }

		/// <summary>
		/// Gets the source path represented by the current instance.
		/// </summary>
		public string Path { get; private set; }

		/// <summary>
		/// Indicates whether the source represents a static property.
		/// </summary>
		public bool IsStatic { get; private set; }

		/// <summary>
		/// Gets the name of the final property along the source path.
		/// </summary>
		public string SourceProperty { get;	private set; }

		/// <summary>
		/// Gets the name of the type that declares the final property along the source path.
		/// </summary>
		public string SourceType { get; private set; }

		/// <summary>
		/// Gets the underlying value of the property for the current source path.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public object GetValue(GraphInstance root)
		{
			IGraphPropertySource source = GetSource(root);
			return source == null ? null : source[SourceProperty];
		}

		/// <summary>
		/// Gets the formatted value of the property for the current source path.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public string GetFormattedValue(GraphInstance root)
		{
			return GetFormattedValue(root, null);
		}

		/// <summary>
		/// Gets the formatted value of the property for the current source path.
		/// </summary>
		/// <param name="root"></param>
		/// <param name="format">The specific format to use</param>
		/// <returns></returns>
		public string GetFormattedValue(GraphInstance root, string format)
		{
			IGraphPropertySource source = GetSource(root);
			return source == null ? null : source.GetFormattedValue(SourceProperty, format);
		}

		/// <summary>
		/// Determines whether the value of the property along the source path has a value or not.
		/// </summary>
		/// <param name="root"></param>
		/// <returns>True if the source path has an assigned value, otherwise false.</returns>
		/// <remarks>
		/// If any value along the source path is null, false will be returned. 
		/// If the source property is a list, false will be returned if the list is empty.
		/// </remarks>
		public bool HasValue(GraphInstance root)
		{
			// Get the source
			IGraphPropertySource source = GetSource(root);

			// Return false if the source is null
			if (source == null)
				return false;

			// Get the property off of the source to evaluate
			GraphProperty property = source.Properties[SourceProperty];

			// If the property is a list, determine if the list has items
			if (property is GraphReferenceProperty && property.IsList)
				return source.GetList((GraphReferenceProperty)property).Count > 0;

			// Otherwise, just determine if the property has an assigned value
			else
				return source[property] != null;
		}

		public IGraphPropertySource GetSource(GraphInstance root)
		{
			// Return the source type for static paths
			if (IsStatic)
				return GraphContext.Current.GetGraphType(SourceType);

			// Otherwise, walk the source path to find the source instance
			foreach (string step in sourcePath)
			{
				if (root == null)
					return null;
				root = root.GetReference(step);
			}

			// Return the source instance
			return root;
		}

		/// <summary>
		/// Gets the <see cref="GraphInstanceList"/> defined by specified source path.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public GraphInstanceList GetList(GraphInstance root)
		{
			IGraphPropertySource source = GetSource(root);
			return source == null ? null : source.GetList(SourceProperty);
		}

		/// <summary>
		/// Gets the <see cref="GraphInstance"/> defined by specified source path.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public GraphInstance GetReference(GraphInstance root)
		{
			IGraphPropertySource source = GetSource(root);
			return source == null ? null : source.GetReference(SourceProperty);
		}

		/// <summary>
		/// Gets the string representation of the source path.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Path;
		}
	}
}
