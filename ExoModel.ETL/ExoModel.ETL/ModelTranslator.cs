using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Bibliography;
using ExoModel;
using System.Text.RegularExpressions;
using System.Web.Query.Dynamic;
using System.ComponentModel;
using System.Threading;
using System.Collections;

namespace ExoModel.ETL
{
	/// <summary>
	/// Translates from one <see cref="ModelType"/> to another using a set of property translation expressions.
	/// </summary>
	public class ModelTranslator
	{
		Func<ModelInstance, ModelInstance> createDestinationInstance;

		Func<ModelProperty, TypeConverter> getValueConverter;

		public ModelType SourceType { get; private set; }

		public ModelType DestinationType { get; private set; }

		public ModelTranslator Parent { get; private set; }

		List<PropertyTranslator> Properties { get; set; }

		/// <summary>
		/// Creates a new <see cref="ModelTranslator"/> to support translation from the specified source <see cref="ModelType"/> 
		/// to the specified destination <see cref="ModelType"/> with a specified set of property translations.
		/// </summary>
		/// <param name="translation"></param>
		public ModelTranslator(ModelType sourceType, ModelType destinationType, ModelTranslator parent = null,IEnumerable<string> mappings = null, Func<ModelProperty, TypeConverter> getValueConverter = null, Func<ModelInstance, ModelInstance> createDestinationInstance = null)
		{
			this.SourceType = sourceType;
			this.DestinationType = destinationType;
			this.Parent = parent;
			this.createDestinationInstance = createDestinationInstance ?? ((source) => DestinationType.Create(source.Id) ?? DestinationType.Create());
			this.getValueConverter = getValueConverter ?? (property => property is ModelValueProperty ? ((ModelValueProperty)property).Converter : null);
			this.Properties = new List<PropertyTranslator>();

			// Create a compiled translation for each property mapping
			if (mappings != null)
			{
				foreach (var mapping in mappings)
				{
					var index = mapping.IndexOf("=");
					if (index < 0)
						throw new ArgumentException("Invalid mapping expression: must be in the form of 'Destination Path = Source Expression'.");

					var sourceExpression = mapping.Substring(index + 1).Trim();
					var destinationPath = mapping.Substring(0, index).Trim();
					AddPropertyTranslation(sourceExpression, destinationPath);
				}
			}
		}

		/// <summary>
		/// Initializes a <see cref="PropertyTranslator"/>
		/// </summary>
		/// <param name="sourceExpression">The source expression</param>
		/// <param name="destinationPath">The destination path</param>
		/// <returns></returns>
		private PropertyTranslator CreatePropertyTranslator(string sourceExpression, string destinationPath)
		{
			// Replace property labels with property names in mapping expressions
			foreach (var property in SourceType.Properties)
				sourceExpression = sourceExpression.Replace("[" + property.Label + "]", property.Name);

			// Create a translation containing compiled source expressions and destination sources
			ModelSource destinationSource;
			ModelProperty destinationProperty;
			if (!ModelSource.TryGetSource(DestinationType, destinationPath, out destinationSource, out destinationProperty))
				throw new ApplicationException(string.Format("ModelSource cannot be found for this path {0}", destinationPath));

			// Add the new value property translation
			return new PropertyTranslator()
			{
				SourceExpression = String.IsNullOrEmpty(sourceExpression) ? null : SourceType.GetExpression(sourceExpression),
				DestinationSource = destinationSource,
				DestinationProperty = destinationProperty,
			};
		}

		/// <summary>
		/// Adds a value <see cref="PropertyTranslator"/> to the current <see cref="ModelTranslator"/>.
		/// </summary>
		/// <param name="sourceExpression">The source expression</param>
		/// <param name="destinationPath">The destination path</param>
		/// <param name="valueConverter">The optional value converter to use</param>
		public void AddPropertyTranslation(string sourceExpression, string destinationPath, TypeConverter valueConverter = null)
		{
			// Create translator
			var translator = CreatePropertyTranslator(sourceExpression, destinationPath);
			translator.ValueConverter = valueConverter ?? getValueConverter(translator.DestinationProperty);

			// Add the new value property translation
			Properties.Add(translator);
		}

		/// <summary>
		/// Adds a value <see cref="PropertyTranslator"/> to the current <see cref="ModelTranslator"/>.
		/// </summary>
		/// <param name="sourceExpression">The source expression</param>
		/// <param name="destinationPath">The destination path</param>
		/// <param name="valueConverter">The optional value converter to use</param>
		public void AddPropertyTranslation(string sourceExpression, string destinationPath, ModelTranslator referenceConverter)
		{
			// Create translator
			var translator = CreatePropertyTranslator(sourceExpression, destinationPath);
			translator.ReferenceConverter = referenceConverter;

			// Add the new value property translation
			Properties.Add(translator);
		}

		/// <summary>
		/// Adds a value <see cref="PropertyTranslator"/> to the current <see cref="ModelTranslator"/>
		/// </summary>
		/// <param name="sourceExpression">The source expression</param>
		/// <param name="destinationPath">The destination path</param>
		/// <param name="delegateConverter">The delegate to use to generate the translated value</param>
		public void AddPropertyTranslation(string sourceExpression, string destinationPath, Func<object, string> delegateConverter)
		{
			if (delegateConverter == null)
				throw new ArgumentException("delegateConverter");

			// Create translator
			var translator = CreatePropertyTranslator(sourceExpression, destinationPath);
			translator.DelegateConverter = delegateConverter;

			// Add the new value property translation
			Properties.Add(translator);
		}

		/// <summary>
		/// Translates the specified instances of the source type into the destination type.
		/// </summary>
		/// <param name="sourceInstances">The list of objects created dynamically that will need to be translated.</param>
		/// <param name="createDestinationInstance">A delegate that creates the destination instance for the specified source instance.</param>
		/// <returns></returns>
		public IEnumerable<ModelInstance> Translate(IEnumerable<ModelInstance> sourceInstances)
		{
			// Translate all source instances
			foreach (ModelInstance source in sourceInstances)
				yield return Translate(source);
		}

		/// <summary>
		/// Translates the specified instance of the source type into the destination type.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="createDestinationInstance"></param>
		/// <returns></returns>
		public ModelInstance Translate(ModelInstance source)
		{
			// Create the destination instance
			var destination = createDestinationInstance(source);

			// Apply each property transaction from source to destination
			foreach (var property in Properties)
			{
				// Invoke the source expression for the mapping
				var value = property.SourceExpression == null ? null : property.SourceExpression.Invoke(source);

				// Translate reference properties
				if (property.ReferenceConverter != null)
				{
					// Translate list properties
					if (property.DestinationProperty.IsList)
					{
						var sourceList = ((IEnumerable)property.SourceExpression.Invoke(source));
						if (sourceList != null)
						{
							var destinationList = property.DestinationSource.GetList(destination);
							foreach (var instance in property.ReferenceConverter.Translate(sourceList.Cast<object>().Select(i => ModelInstance.GetModelInstance(i))))
								destinationList.Add(instance);
						}
					}

					// Translate instance properties
					else
					{
						var sourceInstance = ModelInstance.GetModelInstance(property.SourceExpression.Invoke(source));
						var destinationInstance = property.ReferenceConverter.Translate(sourceInstance);
						property.DestinationSource.SetValue(destination, destinationInstance.Instance, EnsureDestinationInstance);
					}
				}

				// Translate value properties
				else
				{
					if (value != null && property.ValueConverter != null && !((ModelValueProperty)property.DestinationProperty).PropertyType.IsAssignableFrom(value.GetType()))
						value = property.ValueConverter.ConvertFrom(null, Thread.CurrentThread.CurrentCulture, value);
					else if (property.DelegateConverter != null)
						value = property.DelegateConverter(value);

					//the destination property is an enum, see if it is a concrete type
					//and has a converter associated with it.  This is handled outside
					//the normal converter paradigm since enums are reference types in ExoModel
					if (value != null && property.DestinationProperty is IReflectionModelType &&
						((IReflectionModelType)property.DestinationProperty).UnderlyingType.BaseType == typeof(Enum))
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
										((IReflectionModelType)property.DestinationProperty).UnderlyingType;

									var converter = TypeDescriptor.GetConverter(underlyingDestinationType);
									value = converter.ConvertFrom(value);
								}
								break;
						}
					}

					// Set the value on the destination instance
					property.DestinationSource.SetValue(destination, value, EnsureDestinationInstance);
				}
			}

			// Return the translated instance
			return destination;
		}

		/// <summary>
		/// Ensure the destination instance path is valid when nulls are encountered along the path.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="property"></param>
		/// <param name="index"></param>
		bool EnsureDestinationInstance(ModelInstance instance, ModelReferenceProperty property, int index)
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
		}
	}

	/// <summary>
	/// Represents the translation of a property from a source to destination model instance.
	/// </summary>
	public class PropertyTranslator
	{
		internal ModelExpression SourceExpression { get; set; }

		internal ModelSource DestinationSource { get; set; }

		internal ModelProperty DestinationProperty { get; set; }

		internal TypeConverter ValueConverter { get; set; }

		internal ModelTranslator ReferenceConverter { get; set; }

		internal Func<object, string> DelegateConverter { get; set; }
	}
}
