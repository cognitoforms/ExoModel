using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;
using System.Text.RegularExpressions;
using System.Web.Query.Dynamic;

namespace ExoModel.ETL
{
	/// <summary>
	/// This class is responsible for translating source data into
	/// root instances based on a mapping file.
	/// </summary>
	public class MapBasedTranslator : ITranslator
	{
		/// <summary>
		/// This dictionary holds a mapping between invalid property names
		/// from the source type and an auto generated valid property name.
		/// Used by the translator to help translate Expressions as well.
		/// </summary>
		public Dictionary<string, string> ValidPropertyMapping { get; set; }
		public Dictionary<ModelInstance, ModelInstance> mappedObjects { get; set; }

		public MapBasedTranslator()
		{
			ValidPropertyMapping = new Dictionary<string, string>();
			mappedObjects = new Dictionary<ModelInstance, ModelInstance>();
		}

		public string AddTranslation(string source)
		{
			string trans = GetValidPropertyName(source);
			ValidPropertyMapping.Add(trans, source);
			return trans;
		}

		public string GetTranslatedNameFromSourceName(string sourceName)
		{
			if (ValidPropertyMapping.ContainsValue(sourceName))
			{
				///ignoring duplicates for now.
				foreach (KeyValuePair<string, string> pair in ValidPropertyMapping)
				{
					if (sourceName.Equals(pair.Value))
					{
						return pair.Key;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Translates an Expression that contains the old invalid
		/// property names to a new string with valid property paths.
		/// </summary>
		/// <param name="expression">The expression to convert.</param>
		/// <returns></returns>
		public string TranslateExpression(string expression)
		{
			//for every property that has been translated run it through the expression
			foreach (KeyValuePair<string, string> pair in ValidPropertyMapping)
			{
				expression = expression.Replace(pair.Value, pair.Key);
			}

			return expression;
		}

		/// <summary>
		/// Given a property name, convert it so the new name is always valid.
		/// </summary>
		/// <param name="propName">The name of the property to convert.</param>
		/// <returns>Returns a new property name that will be guaranteed to be valid.</returns>
		private string GetValidPropertyName(string propName)
		{
			//the first step is to convert to a valid name
			//just replace everything not a-z0-9 with an underscore
			//and start the property name with an underscore.
			string result = "_" + Regex.Replace(propName, @"[^a-zA-Z0-9]", "_");

			//double check the result to make sure it does not already exist in the mapping hash
			while (ValidPropertyMapping.ContainsKey(result))
			{
				//get the number at the end of the name and inc by one
				string lastChar = result.ToCharArray().Last().ToString();
				int outVarInt = 0;
				if (Int32.TryParse(lastChar, out outVarInt))
				{
					outVarInt++;
					result = result.Replace((outVarInt - 1).ToString(), outVarInt.ToString());
				}
				else
				{
					result += 1;
				}
			}

			return result;
		}

		public IEnumerable<ModelInstance> Translate(ModelType destinationType, ModelType sourceType, IEnumerable<ModelInstance> sourceInstances, IMapping mappingData)
		{
			return Translate(destinationType, sourceType, sourceInstances, mappingData, (t, i, e) => { return t.Create(); });
		}

		/// <summary>
		/// This function will translate the data recrods in an import file
		/// to a list of the final model instances based on the mapping data.
		/// </summary>
		/// <param name="destinationType">The type of model instances that you are translating into.</param>
		/// <param name="sourceType">The type of model instances that you are translating from.</param>
		/// <param name="sourceInstances">The list of objects created dynamically that will need to be translated.</param>
		/// <param name="mappingData">The mapping information to help with the translation.</param>
		/// <returns></returns>
		public IEnumerable<ModelInstance> Translate(ModelType destinationType, ModelType sourceType, IEnumerable<ModelInstance> sourceInstances, IMapping mappingData, Func<ModelType, ModelInstance, string, ModelInstance> initializationDestinationInstance)
		{
			foreach (ModelInstance sourceInstance in sourceInstances)
			{
				if (!mappedObjects.ContainsKey(sourceInstance))
				{
					//using the mapping information translate the source instance
					//into the root instance you are trying to convert to.

					//first step is to create a new object of the root type
					//determine if the id string is apart of the mapping first
					ExpressionToProperty idElement = mappingData.GetIdMappingElement();
					string id = null;
					if (idElement != null)
					{
						string translatedExpression = TranslateExpression(idElement.Expression);
						//get the value from the source instance and set the root instance path
						ModelExpression exp = sourceType.GetExpression(translatedExpression);
						id = (string)exp.Expression.Compile().DynamicInvoke(sourceInstance.Instance);
					}

					ModelInstance destination = initializationDestinationInstance(destinationType, sourceInstance, id);

					//now go through the mapping setting the values of the root object
					foreach (ExpressionToProperty map in mappingData.GetMapping())
					{
						//do nothing for the Id path
						//because it is strictly for creation
						//purposes, and cannot be set anyway.
						if (map.PropertyPath == "Id")
							continue;

						string translatedExpression = TranslateExpression(map.Expression);
						object valueFromSource = null;

						//get the value from the source instance and set the root instance path
						ModelExpression exp = sourceType.GetExpression(translatedExpression);

						//check and see if any parameters exist on the expression to pass to it
						if (exp.Expression.Parameters.Count == 0)
							valueFromSource = exp.CompiledExpression.DynamicInvoke();
						else
							valueFromSource = exp.CompiledExpression.DynamicInvoke(sourceInstance.Instance);

						ModelSource evalPath = new ModelSource(destination.Type, map.PropertyPath);

						//convert the value to it's destination type.
						var destProp = ModelContext.Current.GetModelType(evalPath.SourceType).Properties[evalPath.SourceProperty] as ModelValueProperty;
						if (destProp != null && destProp.Converter != null && valueFromSource != null)
						{
							valueFromSource = destProp.Converter.ConvertFrom(valueFromSource);
						}

						evalPath.SetValue(destination, valueFromSource, (instance, property, index) =>
							{
								//write code to perform initialization of null entities
								//if it is an an list, then initialize blank intstances
								//up to the index passed in.
								if (property.IsList)
								{
									//now create the number of instances up to and including index
									ModelInstanceList list = instance.GetList(property);
									for (int i = list.Count; i <= index; i++)
									{
										list.Add(property.PropertyType.Create());
									}
								}
								else
								{
									instance.SetReference(property, property.PropertyType.Create());
								}

								return true;
							});
					}

					mappedObjects[sourceInstance] = destination;
				}

				yield return mappedObjects[sourceInstance];
			}
		}
	}
}
