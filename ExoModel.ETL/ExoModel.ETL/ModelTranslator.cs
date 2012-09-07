using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;
using System.Text.RegularExpressions;
using System.Web.Query.Dynamic;
using System.ComponentModel;

namespace ExoModel.ETL
{
	/// <summary>
	/// Translates from one <see cref="ModelType"/> to another using a set of <see cref="PropertyMapping"/> expressions.
	/// </summary>
	public class ModelTranslator
	{
		ModelType SourceType { get; set; }

		ModelType DestinationType { get; set; }

		PropertyTranslation[] Translations { get; set; }

		/// <summary>
		/// Creates a new <see cref="ModelTranslator"/> to support translation from the specified source <see cref="ModelType"/> 
		/// to the specified destination <see cref="ModelType"/> with a specified set of property mappings.
		/// </summary>
		/// <param name="sourceType"></param>
		/// <param name="destinationType"></param>
		/// <param name="mappings"></param>
		public ModelTranslator(ModelType sourceType, ModelType destinationType, params string[] mappings)
			: this(sourceType, destinationType, (IEnumerable<string>)mappings)
		{ }

		/// <summary>
		/// Creates a new <see cref="ModelTranslator"/> to support translation from the specified source <see cref="ModelType"/> 
		/// to the specified destination <see cref="ModelType"/> with a specified set of property mappings.
		/// </summary>
		/// <param name="sourceType"></param>
		/// <param name="destinationType"></param>
		/// <param name="mappings"></param>
		public ModelTranslator(ModelType sourceType, ModelType destinationType, IEnumerable<string> mappings)
		{
			this.SourceType = sourceType;
			this.DestinationType = destinationType;

			// Create a compiled translation for each property mapping
			this.Translations = mappings.Select(m =>
			{
				var index = m.IndexOf("=");
				if (index < 0)
					throw new ArgumentException("Invalid mapping expression: must be in the form of 'Destination Path = Source Expression'.");

				var sourceExpression = m.Substring(index + 1).Trim();
				var destinationPath = m.Substring(0, index).Trim();

				// Replace property labels with property names in mapping expressions
				foreach (var property in sourceType.Properties)
					sourceExpression = sourceExpression
						.Replace("[" + property.Label + "]", property.Name)
						.Replace(property.Label, property.Name);

				// Create a translation containing compiled source expressions and destination sources
				var destinationSource = new ModelSource(destinationType, destinationPath);
				var destinationProperty = destinationType.Context.GetModelType(destinationSource.SourceType).Properties[destinationSource.SourceProperty];
				return new PropertyTranslation() {
					SourceExpression = sourceType.GetExpression(sourceExpression),
					DestinationSource = destinationSource,
					DestinationProperty = destinationProperty,
					ValueConverter = 
						destinationProperty is ModelValueProperty && ((ModelValueProperty)destinationProperty).Converter.CanConvertTo(typeof(object)) 
						? ((ModelValueProperty)destinationProperty).Converter : null
				};
			})
			.ToArray();
		}
		
		/// <summary>
		/// Translates the specified instances of the source type into the destination type.
		/// </summary>
		/// <param name="sourceInstances">The list of objects created dynamically that will need to be translated.</param>
		/// <returns></returns>
		public IEnumerable<ModelInstance> Translate(IEnumerable<ModelInstance> sourceInstances)
		{
			return Translate(sourceInstances, (source) => DestinationType.Create(source.Id) ?? DestinationType.Create());
		}

		/// <summary>
		/// Translates the specified instances of the source type into the destination type.
		/// </summary>
		/// <param name="sourceInstances">The list of objects created dynamically that will need to be translated.</param>
		/// <param name="createDestinationInstance">A delegate that creates the destination instance for the specified source instance.</param>
		/// <returns></returns>
		public IEnumerable<ModelInstance> Translate(IEnumerable<ModelInstance> sourceInstances, Func<ModelInstance, ModelInstance> createDestinationInstance)
		{
			// Translate all source instances
			foreach (ModelInstance source in sourceInstances)
				yield return Translate(source, createDestinationInstance);
		}

		/// <summary>
		/// Translates the specified instance of the source type into the destination type.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="createDestinationInstance"></param>
		/// <returns></returns>
		public ModelInstance Translate(ModelInstance source, Func<ModelInstance, ModelInstance> createDestinationInstance)
		{
			// Create the destination instance
			var destination = createDestinationInstance(source);

			// Apply each property transaction from source to destination
			foreach (var translation in Translations)
			{
				// Invoke the source expression for the mapping
				var value =
					translation.SourceExpression.Expression.Parameters.Count == 0 ?
						translation.SourceExpression.CompiledExpression.DynamicInvoke() :
						translation.SourceExpression.CompiledExpression.DynamicInvoke(source.Instance);

				// Handle conversion of value properties
				if (value != null && translation.ValueConverter != null)
					value = translation.ValueConverter.ConvertFrom(value);

				// Set the value on the destination instance
				translation.DestinationSource.SetValue(destination, value,

					// Ensure the destination instance path is valid when nulls are encountered along the path
					(instance, property, index) =>
					{
						if (property.IsList)
						{
							ModelInstanceList list = instance.GetList(property);
							for (int i = list.Count; i <= index; i++)
								list.Add(property.PropertyType.Create());
						}
						else
							instance.SetReference(property, property.PropertyType.Create());

						return true;
					});
			}

			// Return the translated instance
			return destination;
		}

		/// <summary>
		/// Represents the translation of a property from a source to destination model instance.
		/// </summary>
		class PropertyTranslation
		{
			internal ModelExpression SourceExpression { get; set; }

			internal ModelSource DestinationSource { get; set; }

			internal ModelProperty DestinationProperty { get; set; }

			internal TypeConverter ValueConverter { get; set; }
		}
	}
}
