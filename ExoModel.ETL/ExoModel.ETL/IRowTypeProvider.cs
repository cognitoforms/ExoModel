using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.ETL
{
	public interface IRowTypeProvider
	{
		IRowTypeProvider CreateInstance(ModelType type, IEnumerable<object> Row);
		IRowTypeProvider CreateType(IEnumerable<string> columnHeaders, out ModelType dynamicType, string typeName);
		ModelInstance GetModelInstance(ModelType type, string id);
	}
}
