using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.ETL
{
	public interface ITranslator
	{
		string AddTranslation(string sourceName);
		string GetTranslatedNameFromSourceName(string sourceName);
		string TranslateExpression(string expression);
		IEnumerable<ModelInstance> Translate(ModelType destinationType, ModelType sourceType, IEnumerable<ModelInstance> sourceInstances, IMapping mappingData);
		IEnumerable<ModelInstance> Translate(ModelType destinationType, ModelType sourceType, IEnumerable<ModelInstance> sourceInstances, IMapping mappingData, Func<ModelType, ModelInstance, string, ModelInstance> initializeDestinationInstance);
	}
}
