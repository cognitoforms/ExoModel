using System;
using System.Linq;

namespace ExoModel
{
	/// <summary>
	/// Utility class that supports accessing the value of a property along a static or instance source path.
	/// </summary>
	public class ModelSource
	{
		string[] sourcePath;

		ModelSource()
		{ }

		/// <summary>
		/// Creates a new <see cref="ModelSource"/> for the specified root type and path.
		/// </summary>
		/// <param name="rootType">The root type name, which is required for instance paths</param>
		/// <param name="path">The source path, which is either an instance path or a static path</param>
		public ModelSource(ModelPath path)
		{
			InitializeFromModelPath(path);
		}

		/// <summary>
		/// Creates a new <see cref="ModelSource"/> for the specified root type and path.
		/// </summary>
		/// <param name="rootType">The root type name, which is required for instance paths</param>
		/// <param name="path">The source path, which is either an instance path or a static path</param>
		public ModelSource(ModelType rootType, string path)
		{
			// Raise an error if the specified path is not valid
			if (!InitializeFromTypeAndPath(rootType, path))
				throw new ArgumentException("The specified path, '" + path + "', was not valid for the root type of '" + rootType.Name + "'.", "path");
		}

		/// <summary>
		/// Attempts to create a new <see cref="ModelSource"/> for the specified root type and path.
		/// </summary>
		/// <param name="rootType">The root type name, which is required for instance paths</param>
		/// <param name="path">The source path, which is either an instance path or a static path</param>
		/// <returns>True if the source was created, otherwise false</returns>
		public static bool TryGetSource(ModelType rootType, string path, out ModelSource source)
		{
			source = new ModelSource();
			if (source.InitializeFromTypeAndPath(rootType, path))
				return true;
			else
			{
				source = null;
				return false;
			}
		}

		/// <summary>
		/// Attempts to initialize a <see cref="ModelSource"/> with the specified root type and path.
		/// </summary>
		/// <param name="rootType">The root type name, which is required for instance paths</param>
		/// <param name="path">The source path, which is either an instance path or a static path</param>
		/// <returns>True if the source was created, otherwise false</returns>
		bool InitializeFromTypeAndPath(ModelType rootType, string path)
		{
			// Instance Path
			ModelPath instancePath;
			if (rootType != null && rootType.TryGetPath(path, out instancePath))
			{
				InitializeFromModelPath(instancePath);
				return true;
			}

			// Static Path
			else if (path.Contains('.'))
			{
				// Store the source path
				var sourceModelType = ModelContext.Current.GetModelType(path.Substring(0, path.LastIndexOf('.')));
				if (sourceModelType != null)
				{
					var sourceModelProperty = sourceModelType.Properties[path.Substring(path.LastIndexOf('.') + 1)];
					if (sourceModelProperty != null && sourceModelProperty.IsStatic)
					{
						this.Path = path;
						this.IsStatic = true;
						this.SourceProperty = sourceModelProperty.Name;
						this.SourceType = sourceModelProperty.DeclaringType.Name;
						return true;
					}
				}
			}

			// Otherwise, return false to indicate that the source path is not valid
			return false;
		}

		/// <summary>
		/// Initialize the source from a valid <see cref="ModelPath"/>
		/// </summary>
		/// <param name="instancePath"></param>
		private void InitializeFromModelPath(ModelPath instancePath)
		{
			this.Path = instancePath.Path;
			this.IsStatic = false;

			this.RootType = instancePath.RootType.Name;
			this.sourcePath = instancePath.Path.Split('.');
			this.sourcePath = sourcePath.Take(sourcePath.Length - 1).ToArray();

			// Get the last property along the path
			ModelStep step = instancePath.FirstSteps.First();
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
		public object GetValue(ModelInstance root)
		{
			IModelPropertySource source = GetSource(root);
			return source == null ? null : source[SourceProperty];
		}

		/// <summary>
		/// Gets the formatted value of the property for the current source path.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public string GetFormattedValue(ModelInstance root)
		{
			return GetFormattedValue(root, null);
		}

		/// <summary>
		/// Gets the formatted value of the property for the current source path.
		/// </summary>
		/// <param name="root"></param>
		/// <param name="format">The specific format to use</param>
		/// <returns></returns>
		public string GetFormattedValue(ModelInstance root, string format)
		{
			IModelPropertySource source = GetSource(root);
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
		public bool HasValue(ModelInstance root)
		{
			// Get the source
			IModelPropertySource source = GetSource(root);

			// Return false if the source is null
			if (source == null)
				return false;

			// Get the property off of the source to evaluate
			ModelProperty property = source.Properties[SourceProperty];

			// If the property is a list, determine if the list has items
			if (property is ModelReferenceProperty && property.IsList)
				return source.GetList((ModelReferenceProperty)property).Count > 0;

			// Otherwise, just determine if the property has an assigned value
			else
				return source[property] != null;
		}

		public IModelPropertySource GetSource(ModelInstance root)
		{
			// Return the source type for static paths
			if (IsStatic)
				return ModelContext.Current.GetModelType(SourceType);

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
		/// Gets the <see cref="ModelInstanceList"/> defined by specified source path.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public ModelInstanceList GetList(ModelInstance root)
		{
			IModelPropertySource source = GetSource(root);
			return source == null ? null : source.GetList(SourceProperty);
		}

		/// <summary>
		/// Gets the <see cref="ModelInstance"/> defined by specified source path.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public ModelInstance GetReference(ModelInstance root)
		{
			IModelPropertySource source = GetSource(root);
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
