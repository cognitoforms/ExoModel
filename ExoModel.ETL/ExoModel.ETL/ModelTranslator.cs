using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;
using System.Text.RegularExpressions;
using System.Web.Query.Dynamic;
using System.ComponentModel;
using System.Threading;

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

				ModelSource destinationSource;
				if (!ModelSource.TryGetSource(destinationType, destinationPath, out destinationSource))
					throw new ApplicationException(string.Format("ModelSource cannot be found for this path {0}", destinationPath));

				var destinationProperty = destinationType.Context.GetModelType(destinationSource.SourceType).Properties[destinationSource.SourceProperty];
				return new PropertyTranslation() {
					SourceExpression = String.IsNullOrEmpty(sourceExpression) ? null : sourceType.GetExpression(sourceExpression),
					DestinationSource = destinationSource,
					DestinationProperty = destinationProperty,
					ValueConverter = 
						destinationProperty is ModelValueProperty
						? GetConverter((ModelValueProperty)destinationProperty) : null
				};
			})
			.ToArray();
		}

		/// <summary>
		/// Gets the appropriate converter for the specified <see cref="ModelValueProperty"/>.
		/// </summary>
		/// <param name="property"></param>
		/// <returns></returns>
		/// <remarks>
		/// Custom converters can be introduced here to improve the resilency of the import process.  
		/// For example, the default DecimalConverter does not support commas, so is overriden.
		/// </remarks>
		TypeConverter GetConverter(ModelValueProperty property)
		{
			if (property.PropertyType == typeof(decimal) && (property.Converter == null || property.Converter.GetType() == typeof(System.ComponentModel.DecimalConverter)))
				return DecimalConverter.Default;
			if (property.PropertyType == typeof(decimal?) && (property.Converter == null || (property.Converter.GetType() == typeof(NullableConverter) && ((NullableConverter)property.Converter).UnderlyingTypeConverter.GetType() == typeof(System.ComponentModel.DecimalConverter))))
				return NullableDecimalConverter.Default;
			return property.Converter;
		}

		/// <summary>
		/// Adds support for parsing decimals that include thousands separators.
		/// </summary>
		class DecimalConverter : System.ComponentModel.DecimalConverter
		{
			internal static readonly DecimalConverter Default = new DecimalConverter();

			public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
			{
				if (value is string)
					return Decimal.Parse((string)value);
				return base.ConvertFrom(context, culture, value);
			}
		}

		/// <summary>
		/// Adds support for parsing nullable decimals that include thousands separators.
		/// </summary>
		class NullableDecimalConverter : NullableConverter
		{
			internal static readonly NullableDecimalConverter Default = new NullableDecimalConverter();

			NullableDecimalConverter()
				: base(typeof(decimal?))
			{ }

			public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
			{
				if (value is string)
					return String.IsNullOrWhiteSpace((string)value) ? null : (decimal?)Decimal.Parse((string)value);
				return base.ConvertFrom(context, culture, value);
			}
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
				var value = translation.SourceExpression == null ? null : translation.SourceExpression.Invoke(source);

				// Handle conversion of value properties
				if (value != null && translation.ValueConverter != null && !((ModelValueProperty)translation.DestinationProperty).PropertyType.IsAssignableFrom(value.GetType()))
					value = translation.ValueConverter.ConvertFrom(null, Thread.CurrentThread.CurrentCulture, value);

				//the destination property is an enum, see if it is a concrete type
				//and has a converter associated with it.  This is handled outside
				//the normal converter paradigm since enums are reference types in ExoModel
				if (value != null && translation.DestinationProperty is IReflectionModelProperty &&
					((IReflectionModelProperty)translation.DestinationProperty).PropertyInfo.PropertyType.BaseType == typeof(Enum))
				{
					switch (value.ToString())
					{
						//if the enum value is empty just set it to it's default value
						case "":
							value = 0;
							break;
						default:
							{
								var underlyingDestinationType =
									((IReflectionModelProperty)translation.DestinationProperty).PropertyInfo.PropertyType;

								var converter = TypeDescriptor.GetConverter(underlyingDestinationType);
								value = converter.ConvertFrom(value);
							}
							break;
					}
				}

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
