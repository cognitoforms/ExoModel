using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.ETL
{
	public interface IImportFile
	{
		IEnumerable<ModelInstance> GetInstances(ModelType type);
		IEnumerable<ModelType> GetTypesGenerated();
	}
}
