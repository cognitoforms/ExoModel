using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ExoModel
{
	/// <summary>
	/// Represents a property path from a root <see cref="ModelType"/> with one or more steps.
	/// </summary>
	/// <remarks>
	/// Due to inheritance, property paths may branch as properties with similar names appear
	/// on siblings in the inheritance hierarchy.
	/// </remarks>
	public class ModelPath : IDisposable
	{
		#region Fields

		EventHandler<ModelPathChangeEvent> change;

		#endregion

		#region Properties

		/// <summary>
		/// The string path the current instance represents.
		/// </summary>
		public string Path { get; private set; }

		/// <summary>
		/// The root <see cref="ModelType"/> the path starts from.
		/// </summary>
		public ModelType RootType { get; private set; }

		/// <summary>
		/// The first <see cref="Step"/>s along the path.
		/// </summary>
		public ModelStepList FirstSteps { get; private set; }

		/// <summary>
		/// Event that is raised when any property along the path is changed.
		/// </summary>
		public event EventHandler<ModelPathChangeEvent> Change
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
		static void Subscribe(ModelStepList steps)
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
		static void Unsubscribe(ModelStepList steps)
		{
			foreach (var step in steps)
			{
				step.Property.Observers.Remove(step);
				Unsubscribe(step.NextSteps);
			}
		}

		/// <summary>
		/// Creates a new <see cref="ModelPath"/> instance for the specified root <see cref="ModelType"/>
		/// based on the specified <see cref="Expression"/> tree.
		/// </summary>
		/// <param name="rootType"></param>
		/// <param name="expression"></param>
		/// <returns></returns>
		internal static ModelPath CreatePath(ModelType rootType, Expression expression)
		{
			return PathBuilder.Build(rootType, expression);
		}

		/// <summary>
		/// Creates a new <see cref="ModelPath"/> instance for the specified root <see cref="ModelType"/>
		/// and path string.
		/// </summary>
		/// <param name="rootType"></param>
		/// <param name="path"></param>
		internal static ModelPath CreatePath(ModelType rootType, string path)
		{
			// Create the new path
			ModelPath newPath = new ModelPath()
			{
				RootType = rootType
			};

			// Create a temporary root step
			var root = new ModelStep(newPath);

			var steps = new List<ModelStep>() { root };
			var stack = new Stack<List<ModelStep>>();

			int expectedTokenStart = 0;

			var tokenMatches = pathParser.Matches(path);
			int tokenIndex = 0;
			foreach (Match tokenMatch in tokenMatches)
			{
				// ensure there are no gaps between tokens except for whitespace
				if (tokenMatch.Index != expectedTokenStart && path.Substring(expectedTokenStart, tokenMatch.Index - expectedTokenStart - 1).Trim() != "" )
					throw new ArgumentException("The specified path, '" + path + "', is not valid. Character " + (expectedTokenStart));

				expectedTokenStart = tokenMatch.Index + tokenMatch.Length;

				var token = tokenMatch.Value;
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
						var filter = rootType.Context.GetModelType(token.Substring(1, token.Length - 2));
						foreach (var step in steps)
						{
							if (step.Property is ModelReferenceProperty &&
								(((ModelReferenceProperty)step.Property).PropertyType == filter ||
								((ModelReferenceProperty)step.Property).PropertyType.IsSubType(filter)))
								step.Filter = filter;
							else
								RemoveStep(step);
						}
						break;
					default:

						// Get the property name for the next step
						var propertyName = token;

						// Track the next steps
						var nextSteps = new List<ModelStep>();


						// Process each of the current steps
						foreach (var step in steps)
						{
							// Ensure the current step is a valid reference property
							if (step.Property != null && step.Property is ModelValueProperty)
								throw new ArgumentException("Property '" + step.Property.Name + "' is a value property and cannot have child properties specified.");

							// Get the type of the current step
							var currentType = step.Property != null ? ((ModelReferenceProperty)step.Property).PropertyType : step.Path.RootType;
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
								filter = tokenIndex < tokenMatches.Count - 1 && tokenMatches[tokenIndex + 1].Value.StartsWith("<") ?
									rootType.Context.GetModelType(tokenMatches[tokenIndex + 1].Value.Substring(1, tokenMatches[tokenIndex + 1].Length - 2)) :
									null;

								// See if the step already exists for this property and filter or needs to be created
								var nextStep = step.NextSteps.Where(s => s.Property == property && s.Filter == filter).FirstOrDefault();
								if (nextStep == null)
								{
									nextStep = new ModelStep(newPath) { Property = property, Filter = filter, PreviousStep = step.Property != null ? step : null };
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
			
				++tokenIndex;
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
		static void RemoveStep(ModelStep step)
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
		/// Parses query paths into steps for processing
		/// </summary>
		static Regex pathParser = new Regex(@"[_0-9a-zA-Z\u00aa\u00b5\u00ba\u00c0-\u00d6\u00d8-\u00f6\u00f8-\u02b8\u02bb-\u02c1\u02d0-\u02d1\u02e0-\u02e4\u02ee\u0370-\u0373\u0376-\u0377\u037a-\u037d\u0386\u0388-\u038a\u038c\u038e-\u03a1\u03a3-\u03f5\u03f7-\u0481\u048a-\u0523\u0531-\u0556\u0559\u0561-\u0587\u05d0-\u05ea\u05f0-\u05f2\u0621-\u064a\u0660-\u0669\u066e-\u066f\u0671-\u06d3\u06d5\u06e5-\u06e6\u06ee-\u06fc\u06ff\u0710\u0712-\u072f\u074d-\u07a5\u07b1\u07c0-\u07ea\u07f4-\u07f5\u07fa\u0904-\u0939\u093d\u0950\u0958-\u0961\u0966-\u096f\u0971-\u0972\u097b-\u097f\u0985-\u098c\u098f-\u0990\u0993-\u09a8\u09aa-\u09b0\u09b2\u09b6-\u09b9\u09bd\u09ce\u09dc-\u09dd\u09df-\u09e1\u09e6-\u09f1\u0a05-\u0a0a\u0a0f-\u0a10\u0a13-\u0a28\u0a2a-\u0a30\u0a32-\u0a33\u0a35-\u0a36\u0a38-\u0a39\u0a59-\u0a5c\u0a5e\u0a66-\u0a6f\u0a72-\u0a74\u0a85-\u0a8d\u0a8f-\u0a91\u0a93-\u0aa8\u0aaa-\u0ab0\u0ab2-\u0ab3\u0ab5-\u0ab9\u0abd\u0ad0\u0ae0-\u0ae1\u0ae6-\u0aef\u0b05-\u0b0c\u0b0f-\u0b10\u0b13-\u0b28\u0b2a-\u0b30\u0b32-\u0b33\u0b35-\u0b39\u0b3d\u0b5c-\u0b5d\u0b5f-\u0b61\u0b66-\u0b6f\u0b71\u0b83\u0b85-\u0b8a\u0b8e-\u0b90\u0b92-\u0b95\u0b99-\u0b9a\u0b9c\u0b9e-\u0b9f\u0ba3-\u0ba4\u0ba8-\u0baa\u0bae-\u0bb9\u0bd0\u0be6-\u0bef\u0c05-\u0c0c\u0c0e-\u0c10\u0c12-\u0c28\u0c2a-\u0c33\u0c35-\u0c39\u0c3d\u0c58-\u0c59\u0c60-\u0c61\u0c66-\u0c6f\u0c85-\u0c8c\u0c8e-\u0c90\u0c92-\u0ca8\u0caa-\u0cb3\u0cb5-\u0cb9\u0cbd\u0cde\u0ce0-\u0ce1\u0ce6-\u0cef\u0d05-\u0d0c\u0d0e-\u0d10\u0d12-\u0d28\u0d2a-\u0d39\u0d3d\u0d60-\u0d61\u0d66-\u0d6f\u0d7a-\u0d7f\u0d85-\u0d96\u0d9a-\u0db1\u0db3-\u0dbb\u0dbd\u0dc0-\u0dc6\u0e01-\u0e30\u0e32-\u0e33\u0e40-\u0e46\u0e50-\u0e59\u0e81-\u0e82\u0e84\u0e87-\u0e88\u0e8a\u0e8d\u0e94-\u0e97\u0e99-\u0e9f\u0ea1-\u0ea3\u0ea5\u0ea7\u0eaa-\u0eab\u0ead-\u0eb0\u0eb2-\u0eb3\u0ebd\u0ec0-\u0ec4\u0ec6\u0ed0-\u0ed9\u0edc-\u0edd\u0f00\u0f20-\u0f29\u0f40-\u0f47\u0f49-\u0f6c\u0f88-\u0f8b\u1000-\u102a\u103f-\u1049\u1050-\u1055\u105a-\u105d\u1061\u1065-\u1066\u106e-\u1070\u1075-\u1081\u108e\u1090-\u1099\u10a0-\u10c5\u10d0-\u10fa\u10fc\u1100-\u1159\u115f-\u11a2\u11a8-\u11f9\u1200-\u1248\u124a-\u124d\u1250-\u1256\u1258\u125a-\u125d\u1260-\u1288\u128a-\u128d\u1290-\u12b0\u12b2-\u12b5\u12b8-\u12be\u12c0\u12c2-\u12c5\u12c8-\u12d6\u12d8-\u1310\u1312-\u1315\u1318-\u135a\u1380-\u138f\u13a0-\u13f4\u1401-\u166c\u166f-\u1676\u1681-\u169a\u16a0-\u16ea\u1700-\u170c\u170e-\u1711\u1720-\u1731\u1740-\u1751\u1760-\u176c\u176e-\u1770\u1780-\u17b3\u17d7\u17dc\u17e0-\u17e9\u1810-\u1819\u1820-\u1877\u1880-\u18a8\u18aa\u1900-\u191c\u1946-\u196d\u1970-\u1974\u1980-\u19a9\u19c1-\u19c7\u19d0-\u19d9\u1a00-\u1a16\u1b05-\u1b33\u1b45-\u1b4b\u1b50-\u1b59\u1b83-\u1ba0\u1bae-\u1bb9\u1c00-\u1c23\u1c40-\u1c49\u1c4d-\u1c7d\u1d00-\u1dbf\u1e00-\u1f15\u1f18-\u1f1d\u1f20-\u1f45\u1f48-\u1f4d\u1f50-\u1f57\u1f59\u1f5b\u1f5d\u1f5f-\u1f7d\u1f80-\u1fb4\u1fb6-\u1fbc\u1fbe\u1fc2-\u1fc4\u1fc6-\u1fcc\u1fd0-\u1fd3\u1fd6-\u1fdb\u1fe0-\u1fec\u1ff2-\u1ff4\u1ff6-\u1ffc\u2071\u207f\u2090-\u2094\u2102\u2107\u210a-\u2113\u2115\u2119-\u211d\u2124\u2126\u2128\u212a-\u212d\u212f-\u2139\u213c-\u213f\u2145-\u2149\u214e\u2183-\u2184\u2c00-\u2c2e\u2c30-\u2c5e\u2c60-\u2c6f\u2c71-\u2c7d\u2c80-\u2ce4\u2d00-\u2d25\u2d30-\u2d65\u2d6f\u2d80-\u2d96\u2da0-\u2da6\u2da8-\u2dae\u2db0-\u2db6\u2db8-\u2dbe\u2dc0-\u2dc6\u2dc8-\u2dce\u2dd0-\u2dd6\u2dd8-\u2dde\u3005-\u3006\u3031-\u3035\u303b-\u303c\u3041-\u3096\u309d-\u309f\u30a1-\u30fa\u30fc-\u30ff\u3105-\u312d\u3131-\u318e\u31a0-\u31b7\u31f0-\u31ff\u3400-\u4db5\u4e00-\u9fc3\ua000-\ua48c\ua500-\ua60c\ua610-\ua62b\ua640-\ua65f\ua662-\ua66e\ua680-\ua697\ua722-\ua788\ua78b-\ua78c\ua7fb-\ua801\ua803-\ua805\ua807-\ua80a\ua80c-\ua822\ua840-\ua873\ua882-\ua8b3\ua8d0-\ua8d9\ua900-\ua925\ua930-\ua946\uaa00-\uaa28\uaa40-\uaa42\uaa44-\uaa4b\uaa50-\uaa59\uac00-\ud7a3\uf900-\ufa2d\ufa30-\ufa6a\ufa70-\ufad9\ufb00-\ufb06\ufb13-\ufb17\ufb1d\ufb1f-\ufb28\ufb2a-\ufb36\ufb38-\ufb3c\ufb3e\ufb40-\ufb41\ufb43-\ufb44\ufb46-\ufbb1\ufbd3-\ufd3d\ufd50-\ufd8f\ufd92-\ufdc7\ufdf0-\ufdfb\ufe70-\ufe74\ufe76-\ufefc\uff10-\uff19\uff21-\uff3a\uff41-\uff5a\uff66-\uffbe\uffc2-\uffc7\uffca-\uffcf\uffd2-\uffd7\uffda-\uffdc]+|[{}.,]|\<[._0-9a-zA-Z\u00aa\u00b5\u00ba\u00c0-\u00d6\u00d8-\u00f6\u00f8-\u02b8\u02bb-\u02c1\u02d0-\u02d1\u02e0-\u02e4\u02ee\u0370-\u0373\u0376-\u0377\u037a-\u037d\u0386\u0388-\u038a\u038c\u038e-\u03a1\u03a3-\u03f5\u03f7-\u0481\u048a-\u0523\u0531-\u0556\u0559\u0561-\u0587\u05d0-\u05ea\u05f0-\u05f2\u0621-\u064a\u0660-\u0669\u066e-\u066f\u0671-\u06d3\u06d5\u06e5-\u06e6\u06ee-\u06fc\u06ff\u0710\u0712-\u072f\u074d-\u07a5\u07b1\u07c0-\u07ea\u07f4-\u07f5\u07fa\u0904-\u0939\u093d\u0950\u0958-\u0961\u0966-\u096f\u0971-\u0972\u097b-\u097f\u0985-\u098c\u098f-\u0990\u0993-\u09a8\u09aa-\u09b0\u09b2\u09b6-\u09b9\u09bd\u09ce\u09dc-\u09dd\u09df-\u09e1\u09e6-\u09f1\u0a05-\u0a0a\u0a0f-\u0a10\u0a13-\u0a28\u0a2a-\u0a30\u0a32-\u0a33\u0a35-\u0a36\u0a38-\u0a39\u0a59-\u0a5c\u0a5e\u0a66-\u0a6f\u0a72-\u0a74\u0a85-\u0a8d\u0a8f-\u0a91\u0a93-\u0aa8\u0aaa-\u0ab0\u0ab2-\u0ab3\u0ab5-\u0ab9\u0abd\u0ad0\u0ae0-\u0ae1\u0ae6-\u0aef\u0b05-\u0b0c\u0b0f-\u0b10\u0b13-\u0b28\u0b2a-\u0b30\u0b32-\u0b33\u0b35-\u0b39\u0b3d\u0b5c-\u0b5d\u0b5f-\u0b61\u0b66-\u0b6f\u0b71\u0b83\u0b85-\u0b8a\u0b8e-\u0b90\u0b92-\u0b95\u0b99-\u0b9a\u0b9c\u0b9e-\u0b9f\u0ba3-\u0ba4\u0ba8-\u0baa\u0bae-\u0bb9\u0bd0\u0be6-\u0bef\u0c05-\u0c0c\u0c0e-\u0c10\u0c12-\u0c28\u0c2a-\u0c33\u0c35-\u0c39\u0c3d\u0c58-\u0c59\u0c60-\u0c61\u0c66-\u0c6f\u0c85-\u0c8c\u0c8e-\u0c90\u0c92-\u0ca8\u0caa-\u0cb3\u0cb5-\u0cb9\u0cbd\u0cde\u0ce0-\u0ce1\u0ce6-\u0cef\u0d05-\u0d0c\u0d0e-\u0d10\u0d12-\u0d28\u0d2a-\u0d39\u0d3d\u0d60-\u0d61\u0d66-\u0d6f\u0d7a-\u0d7f\u0d85-\u0d96\u0d9a-\u0db1\u0db3-\u0dbb\u0dbd\u0dc0-\u0dc6\u0e01-\u0e30\u0e32-\u0e33\u0e40-\u0e46\u0e50-\u0e59\u0e81-\u0e82\u0e84\u0e87-\u0e88\u0e8a\u0e8d\u0e94-\u0e97\u0e99-\u0e9f\u0ea1-\u0ea3\u0ea5\u0ea7\u0eaa-\u0eab\u0ead-\u0eb0\u0eb2-\u0eb3\u0ebd\u0ec0-\u0ec4\u0ec6\u0ed0-\u0ed9\u0edc-\u0edd\u0f00\u0f20-\u0f29\u0f40-\u0f47\u0f49-\u0f6c\u0f88-\u0f8b\u1000-\u102a\u103f-\u1049\u1050-\u1055\u105a-\u105d\u1061\u1065-\u1066\u106e-\u1070\u1075-\u1081\u108e\u1090-\u1099\u10a0-\u10c5\u10d0-\u10fa\u10fc\u1100-\u1159\u115f-\u11a2\u11a8-\u11f9\u1200-\u1248\u124a-\u124d\u1250-\u1256\u1258\u125a-\u125d\u1260-\u1288\u128a-\u128d\u1290-\u12b0\u12b2-\u12b5\u12b8-\u12be\u12c0\u12c2-\u12c5\u12c8-\u12d6\u12d8-\u1310\u1312-\u1315\u1318-\u135a\u1380-\u138f\u13a0-\u13f4\u1401-\u166c\u166f-\u1676\u1681-\u169a\u16a0-\u16ea\u1700-\u170c\u170e-\u1711\u1720-\u1731\u1740-\u1751\u1760-\u176c\u176e-\u1770\u1780-\u17b3\u17d7\u17dc\u17e0-\u17e9\u1810-\u1819\u1820-\u1877\u1880-\u18a8\u18aa\u1900-\u191c\u1946-\u196d\u1970-\u1974\u1980-\u19a9\u19c1-\u19c7\u19d0-\u19d9\u1a00-\u1a16\u1b05-\u1b33\u1b45-\u1b4b\u1b50-\u1b59\u1b83-\u1ba0\u1bae-\u1bb9\u1c00-\u1c23\u1c40-\u1c49\u1c4d-\u1c7d\u1d00-\u1dbf\u1e00-\u1f15\u1f18-\u1f1d\u1f20-\u1f45\u1f48-\u1f4d\u1f50-\u1f57\u1f59\u1f5b\u1f5d\u1f5f-\u1f7d\u1f80-\u1fb4\u1fb6-\u1fbc\u1fbe\u1fc2-\u1fc4\u1fc6-\u1fcc\u1fd0-\u1fd3\u1fd6-\u1fdb\u1fe0-\u1fec\u1ff2-\u1ff4\u1ff6-\u1ffc\u2071\u207f\u2090-\u2094\u2102\u2107\u210a-\u2113\u2115\u2119-\u211d\u2124\u2126\u2128\u212a-\u212d\u212f-\u2139\u213c-\u213f\u2145-\u2149\u214e\u2183-\u2184\u2c00-\u2c2e\u2c30-\u2c5e\u2c60-\u2c6f\u2c71-\u2c7d\u2c80-\u2ce4\u2d00-\u2d25\u2d30-\u2d65\u2d6f\u2d80-\u2d96\u2da0-\u2da6\u2da8-\u2dae\u2db0-\u2db6\u2db8-\u2dbe\u2dc0-\u2dc6\u2dc8-\u2dce\u2dd0-\u2dd6\u2dd8-\u2dde\u3005-\u3006\u3031-\u3035\u303b-\u303c\u3041-\u3096\u309d-\u309f\u30a1-\u30fa\u30fc-\u30ff\u3105-\u312d\u3131-\u318e\u31a0-\u31b7\u31f0-\u31ff\u3400-\u4db5\u4e00-\u9fc3\ua000-\ua48c\ua500-\ua60c\ua610-\ua62b\ua640-\ua65f\ua662-\ua66e\ua680-\ua697\ua722-\ua788\ua78b-\ua78c\ua7fb-\ua801\ua803-\ua805\ua807-\ua80a\ua80c-\ua822\ua840-\ua873\ua882-\ua8b3\ua8d0-\ua8d9\ua900-\ua925\ua930-\ua946\uaa00-\uaa28\uaa40-\uaa42\uaa44-\uaa4b\uaa50-\uaa59\uac00-\ud7a3\uf900-\ufa2d\ufa30-\ufa6a\ufa70-\ufad9\ufb00-\ufb06\ufb13-\ufb17\ufb1d\ufb1f-\ufb28\ufb2a-\ufb36\ufb38-\ufb3c\ufb3e\ufb40-\ufb41\ufb43-\ufb44\ufb46-\ufbb1\ufbd3-\ufd3d\ufd50-\ufd8f\ufd92-\ufdc7\ufdf0-\ufdfb\ufe70-\ufe74\ufe76-\ufefc\uff10-\uff19\uff21-\uff3a\uff41-\uff5a\uff66-\uffbe\uffc2-\uffc7\uffca-\uffcf\uffd2-\uffd7\uffda-\uffdc]+\>", RegexOptions.Compiled);

		/// <summary>
		/// Notify path subscribers that the path has changed.
		/// </summary>
		/// <param name="instance"></param>
		internal void Notify(ModelInstance instance)
		{
			if (change != null)
				change(this, new ModelPathChangeEvent(instance, this));
		}

		/// <summary>
		/// Gets the model for the specified root object including all objects on the path.
		/// </summary>
		/// <param name="root"></param>
		/// <returns></returns>
		public HashSet<ModelInstance> GetInstances(ModelInstance root)
		{
			var model = new HashSet<ModelInstance>();
			GetInstances(root, FirstSteps, model);
			return model;
		}

		/// <summary>
		/// Recursively loads a path in the model by walking steps.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="step"></param>
		/// <param name="model"></param>
		void GetInstances(ModelInstance instance, ModelStepList steps, HashSet<ModelInstance> model)
		{
			// Add the instance to the model
			model.Add(instance);

			// Process each child step
			foreach (var step in steps)
			{
				// Process each instance
				foreach (var child in step.GetInstances(instance))
					GetInstances(child, step.NextSteps, model);
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

		#region PathBuilder

		/// <summary>
		/// Builds a <see cref="ModelPath"/> based on the specified <see cref="Expression"/>.
		/// </summary>
		class PathBuilder : ModelExpression.ExpressionVisitor
		{
			Dictionary<Expression, ModelStep> steps = new Dictionary<Expression, ModelStep>();
			Expression expression;
			ModelPath path;
			ModelStep rootStep;

			internal static ModelPath Build(ModelType rootType, Expression expression)
			{
				var builder = new PathBuilder() { expression = expression, path = new ModelPath() { RootType = rootType } };
				builder.Build();
				return builder.path;
			}

			void Build()
			{
				Visit(expression);
				path.FirstSteps = rootStep != null ? rootStep.NextSteps : new ModelStepList();
				if (path.FirstSteps.Count() == 0)
					path.Path = "";
				else if (path.FirstSteps.Count() == 1)
					path.Path = path.FirstSteps.First().ToString();
				else
					path.Path = "{" + path.FirstSteps.Aggregate("", (p, s) => p.Length > 0 ? p + "," + s : s.ToString()) + "}";
			}

			protected override Expression VisitParameter(ParameterExpression p)
			{
				base.VisitParameter(p);
				
				// Add the root step
				if (rootStep == null)
				{
					rootStep = new ModelStep(path);
					steps.Add(p, rootStep);
				}

				return p;
			}

			protected override Expression VisitModelParameter(ModelExpression.ModelParameterExpression p)
			{
				// Add the root step
				if (rootStep == null)
				{
					rootStep = new ModelStep(path);
					steps.Add(p, rootStep);
				}

				return p;
			}

			protected override Expression VisitModelMember(ModelExpression.ModelMemberExpression m)
			{
				base.VisitModelMember(m);
				ModelStep step;
				if (steps.TryGetValue(m.Expression, out step) && !(step.Property is ModelValueProperty))
				{
					// Get the model type of the parent expression
					var type = step.Property == null ? path.RootType : ((ModelReferenceProperty)step.Property).PropertyType;

					// Make sure the type of the expression matches the declaring type of the property
					if (type != m.Property.DeclaringType && !m.Property.DeclaringType.IsSubType(type))
						return m;

					// Determine if the member access represents a property on the model type
					var property = m.Property;

					// Create and record a new step
					var nextStep = step.NextSteps.FirstOrDefault(s => s.Property == property);
					if (nextStep == null)
					{
						nextStep = new ModelStep(path) { Property = property, PreviousStep = step };
						step.NextSteps.Add(nextStep);
					}
					if (!steps.ContainsKey(m))
						steps.Add(m, nextStep);
				}
				return m;
			}

			protected override Expression VisitMemberAccess(MemberExpression m)
			{
				base.VisitMemberAccess(m);
				ModelStep step;
				if (m.Expression != null && steps.TryGetValue(m.Expression, out step) && !(step.Property is ModelValueProperty))
				{
					// Get the model type of the parent expression
					var type = step.Property == null ? path.RootType : ((ModelReferenceProperty)step.Property).PropertyType;

					// Determine if the member access represents a property on the model type
					var property = type.Properties[m.Member.Name];

					// If the property exists on the model type, create and record a new step
					if (property != null)
					{
						var nextStep = step.NextSteps.FirstOrDefault(s => s.Property == property);
						if (nextStep == null)
						{
							nextStep = new ModelStep(path) { Property = property, PreviousStep = step };
							step.NextSteps.Add(nextStep);
						}
						steps.Add(m, nextStep);
					}
				}
				return m;
			}

			static MethodInfo modelInstanceToString = typeof(ModelInstance).GetMethod("ToString", new Type[] { typeof(string) });

			protected override Expression VisitMethodCall(MethodCallExpression m)
			{
				// Visit the target of the method
				Visit(m.Object);

				// Process arguments to method calls to handle lambda expressions
				foreach (var argument in m.Arguments)
				{
					// Perform special logic for lambdas
					if (argument is LambdaExpression || argument is ModelExpression.ModelLambdaExpression)
					{
						// Get the target of the method, assuming for static methods it will be the first argument
						// This handles the common case of extension methods, whose first parameter must be the target instance
						var target = m.Object ?? m.Arguments.First();

						// Determine if the target implements IEnumerable<T>, and if so, determine the type of T
						var listType = target.Type.IsGenericType && target.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ? target.Type :
							target.Type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)).FirstOrDefault();

						// If the instance or first parameter to a static method is represented in the expression path,
						// the corresponding step is a reference list, and the lambda is an action or function accepting instances 
						// from the list, assume that each instance in the list will be passed to the lambda expression
						ModelStep step;
						if (listType != null && steps.TryGetValue(target, out step) && step.Property is ModelReferenceProperty && step.Property.IsList)
						{
							// Find the parameter that will be passed elements from the list, and link it to the parent step
							var parameters = argument is LambdaExpression ? 
								(IEnumerable<Expression>)((LambdaExpression)argument).Parameters : 
								(IEnumerable<Expression>)((ModelExpression.ModelLambdaExpression)argument).Parameters;
							var element = parameters.FirstOrDefault(p => listType.GetGenericArguments()[0].IsAssignableFrom(p.Type));
							if (element != null)
								steps.Add(element, step);

							// If the method return the original list, associate the step with the return value
							if (m.Type.IsAssignableFrom(target.Type))
								steps.Add(m, step);
						}
					}
					Visit(argument);
				}

				// Determine if ToString is being called on a model reference property
				if (m.Method == modelInstanceToString)
				{
					if (m.Object is MemberExpression)
					{
						var memberExpression = (MemberExpression)m.Object;
						if (memberExpression.Member.Name == "Instance" && memberExpression.Expression is UnaryExpression)
						{
							var unaryExpression = (UnaryExpression)memberExpression.Expression;
							if (unaryExpression.Operand is ModelExpression.ModelMemberExpression)
							{
								var propertyExpression = unaryExpression.Operand as ModelExpression.ModelMemberExpression;
								var contantExpression = m.Arguments[0] as ConstantExpression;

								ModelStep step;
								if (steps.TryGetValue(propertyExpression, out step))
								{
									var referenceProperty = (ModelReferenceProperty)propertyExpression.Property;
									referenceProperty.PropertyType.AddFormatSteps(step, (string)contantExpression.Value);
								}
							}
						}
					}
				}
				return m;
			}

			protected override Expression VisitModelCastExpression(ModelExpression.ModelCastExpression m)
			{
				// Visit the target of the method
				Visit(m.Expression.Object);

				// Process arguments to method calls to handle lambda expressions
				foreach (var argument in m.Expression.Arguments)
				{
					// Perform special logic for lambdas
					if (argument is LambdaExpression || argument is ModelExpression.ModelLambdaExpression)
					{
						// Get the target of the method, assuming for static methods it will be the first argument
						// This handles the common case of extension methods, whose first parameter must be the target instance
						var target = m.Expression.Object ?? m.Expression.Arguments.First();

						// Determine if the target implements IEnumerable<T>, and if so, determine the type of T
						var listType = target.Type.IsGenericType && target.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ? target.Type :
							target.Type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)).FirstOrDefault();

						// If the instance or first parameter to a static method is represented in the expression path,
						// the corresponding step is a reference list, and the lambda is an action or function accepting instances 
						// from the list, assume that each instance in the list will be passed to the lambda expression
						ModelStep step;
						if (listType != null && steps.TryGetValue(target, out step) && step.Property is ModelReferenceProperty && step.Property.IsList)
						{
							// Find the parameter that will be passed elements from the list, and link it to the parent step
							var parameters = argument is LambdaExpression ?
								(IEnumerable<Expression>)((LambdaExpression)argument).Parameters :
								(IEnumerable<Expression>)((ModelExpression.ModelLambdaExpression)argument).Parameters;
							var element = parameters.FirstOrDefault(p => listType.GetGenericArguments()[0].IsAssignableFrom(p.Type));
							if (element != null)
								steps.Add(element, step);

							// If the method return the original list, associate the step with the return value
							if (m.Type.IsAssignableFrom(target.Type))
								steps.Add(m, step);
						}
					}
					Visit(argument);
				}
				return m;
			}

			protected override Expression VisitUnary(UnaryExpression u)
			{
				Expression exp = base.VisitUnary(u);

				if (u.NodeType != ExpressionType.Convert && 
					u.NodeType != ExpressionType.ConvertChecked && 
					u.NodeType != ExpressionType.TypeAs)
					return exp;

				ModelStep step;
				if (u.Operand != null && steps.TryGetValue(u.Operand, out step))
				{
					steps.Add(u, step);
				}
				return exp;
			}
		}

		#endregion
	}
}
