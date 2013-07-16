using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExoModel
{
	/// <summary>
	/// Utility class that supports accessing the value of a property along a static or instance source path.
	/// </summary>
	public class ModelSource
	{
		SourceStep [] steps;
		static Regex pathValidation = new Regex(@"^(?<Property>[a-zA-Z0-9_]+)(\[(?<Index>\d+)\])?(\.(?<Property>[a-zA-Z0-9_]+)(\[(?<Index>\d+)\])?)*$", RegexOptions.Compiled);
		static Regex tokenizer = new Regex(@"(?<Property>[a-zA-Z0-9_]+)(\[(?<Index>\d+)\])?", RegexOptions.Compiled);
		static Regex arraySyntaxRegex = new Regex(@"\[\d+\]", RegexOptions.Compiled);

		ModelSource()
		{ }

		/// <summary>
		/// Creates a new <see cref="ModelSource"/> for the specified root type and path.
		/// </summary>
		/// <param name="path">The source model path</param>
		public ModelSource(ModelPath path)
		{
			ModelProperty property;
			InitializeFromModelPath(path, path.Path, out property);
		}

		/// <summary>
		/// Creates a new <see cref="ModelSource"/> for the specified root type and path.
		/// </summary>
		/// <param name="rootType">The root type name, which is required for instance paths</param>
		/// <param name="path">The source path, which is either an instance path or a static path</param>
		public ModelSource(ModelType rootType, string path)
		{
			// Raise an error if the specified path is not valid
			if (!pathValidation.IsMatch(path))
				throw new ArgumentException("The specified path, '" + path + "', was not valid path syntax.");

			// Raise an error if the specified path is not valid
			ModelProperty property;
			if (!InitializeFromTypeAndPath(rootType, path, out property))
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
			ModelProperty property;
			if (source.InitializeFromTypeAndPath(rootType, path, out property))
				return true;
			else
			{
				source = null;
				return false;
			}
		}

		/// <summary>
		/// Attempts to create a new <see cref="ModelSource"/> for the specified root type and path.
		/// </summary>
		/// <param name="rootType">The root type name, which is required for instance paths</param>
		/// <param name="path">The source path, which is either an instance path or a static path</param>
		/// <returns>True if the source was created, otherwise false</returns>
		public static bool TryGetSource(ModelType rootType, string path, out ModelSource source, out ModelProperty sourceProperty)
		{
			source = new ModelSource();
			if (source.InitializeFromTypeAndPath(rootType, path, out sourceProperty))
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
		bool InitializeFromTypeAndPath(ModelType rootType, string path, out ModelProperty sourceProperty)
		{
			// Instance Path
			ModelPath instancePath = null;
			sourceProperty = null;

			// clean up any array indices
			string indexFreePath = arraySyntaxRegex.Replace(path, "");
			try
			{
				if (rootType != null)
					rootType.TryGetPath(indexFreePath, out instancePath);
			}
			catch
			{ }
			if (instancePath != null)
			{
				InitializeFromModelPath(instancePath, path, out sourceProperty);
				return true;
			}

			// Static Path
			else if (path.Contains('.'))
			{
				// Store the source path
				var sourceModelType = ModelContext.Current.GetModelType(path.Substring(0, path.LastIndexOf('.')));
				if (sourceModelType != null)
				{
					sourceProperty = sourceModelType.Properties[path.Substring(path.LastIndexOf('.') + 1)];
					if (sourceProperty != null && sourceProperty.IsStatic)
					{
						this.Path = path;
						this.IsStatic = true;
						this.SourceProperty = sourceProperty.Name;
						this.SourceType = sourceProperty.DeclaringType.Name;
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Initialize the source from a valid <see cref="ModelPath"/>
		/// </summary>
		/// <param name="instancePath"></param>
		void InitializeFromModelPath(ModelPath instancePath, string path, out ModelProperty sourceProperty)
		{
			this.Path = path;
			this.IsStatic = false;
			this.RootType = instancePath.RootType.Name;
			var tokens = tokenizer.Matches(path);
			this.steps = new SourceStep[tokens.Count];
			var rootType = instancePath.RootType;
			int i = 0;
			sourceProperty = null;
			foreach(Match token in tokens)
			{
				sourceProperty = rootType.Properties[token.Groups["Property"].Value];
				if (sourceProperty == null)
					throw new ArgumentException(String.Format("Property {0} is not valid for type {1} in path {2}.", token.Groups["Property"].Value, rootType.Name, path));
				int index;
				if (!Int32.TryParse(token.Groups["Index"].Value, out index))
					index = -1;

				steps[i] = new SourceStep() { Property = sourceProperty.Name, Index = index, DeclaringType = sourceProperty.DeclaringType.Name, IsReferenceProperty = sourceProperty is ModelReferenceProperty };
				rootType = sourceProperty is ModelReferenceProperty ? ((ModelReferenceProperty)sourceProperty).PropertyType : null;
				i++;
			}

			this.SourceProperty = sourceProperty.Name;
			this.SourceType = sourceProperty.DeclaringType.Name;
		}

		/// <summary>
		/// Gets the name of the type that is the starting point for the source path.
		/// </summary>
		public string RootType { get; private set; }

		/// <summary>
		/// Gets the source path represented by the current instance.  array indices removed.
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


		Type underlyingType;
		public Type UnderlyingType
		{
			get
			{
				if (underlyingType == null)
				{
					var modelProperty = ModelContext.Current.GetModelType(SourceType).Properties[SourceProperty];
					if (modelProperty is ModelValueProperty)
						underlyingType = ((ModelValueProperty)modelProperty).PropertyType;
					else if (modelProperty is IReflectionModelProperty)
						underlyingType = ((IReflectionModelProperty)modelProperty).PropertyInfo.PropertyType;

					if (underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() == typeof(Nullable<>))
						underlyingType = Nullable.GetUnderlyingType(underlyingType);
				}

				return underlyingType;
			}
		}

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

		public bool SetValue(ModelInstance root, object value)
		{
			return SetValue(root, value, (i, p, n) => false);
		}

		public bool SetValue(ModelInstance root, object value, Func<ModelInstance, ModelReferenceProperty, int, bool> whenNull)
		{
			IModelPropertySource sourceInstance = GetSource(root, whenNull);
			sourceInstance[SourceProperty] = value;
			return true;
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
			return GetSource(root, (i, p, n) => false);
		}

		public IModelPropertySource GetSource(ModelInstance root, Func<ModelInstance, ModelReferenceProperty, int, bool> whenNull)
		{
			// Return the source type for static paths
			if (IsStatic)
				return ModelContext.Current.GetModelType(SourceType);

			//walk the path performing whenNull if an entity is null
			//if an entity is null and whenNull returns false
			//then exit out of SetValue returning false
			foreach (SourceStep step in this.steps.Take(this.steps.Length - 1))
			{
				ModelReferenceProperty stepProp = (ModelReferenceProperty)ModelContext.Current.GetModelType(step.DeclaringType).Properties[step.Property];
				if (stepProp.IsList)
				{
					ModelInstanceList list = root.GetList(stepProp);
					if (list.Count < step.Index + 1 && !whenNull(root, stepProp, step.Index))
						return null;

					root = list[step.Index];
				}
				else
				{
					if (root.GetReference(stepProp) == null && !whenNull(root, stepProp, step.Index))
						return null;
					else
					{
						//advance to the next step in the chain.
						root = root.GetReference(step.Property);
					}
				}
			}

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

		class SourceStep
		{
			internal string Property { get; set; }
			internal int Index { get; set; }
			internal string DeclaringType { get; set; }
			internal bool IsReferenceProperty { get; set; }
		}
	}
}
