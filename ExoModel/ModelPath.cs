using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using System.Collections.ObjectModel;

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
		static Regex pathParser = new Regex(@"[a-zA-Z0-9_]+|[{}.,]|\<[a-zA-Z0-9_.]+\>", RegexOptions.Compiled);

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

			protected override Expression VisitMethodCall(MethodCallExpression m)
			{
				// Visit the target of the method
				Visit(m.Object);

				// Process arguments to method calls to handle lambda expressions
				foreach (var argument in m.Arguments)
				{
					// Perform special logic for lambdas
					if (argument is LambdaExpression)
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
							var element = ((LambdaExpression)argument).Parameters.FirstOrDefault(p => listType.GetGenericArguments()[0].IsAssignableFrom(p.Type));
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
		}

		#endregion
	}
}
