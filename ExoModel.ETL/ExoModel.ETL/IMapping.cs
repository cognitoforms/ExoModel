using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using ExoModel;

namespace ExoModel.ETL
{
	public interface IMapping
	{
		IEnumerable<ExpressionToProperty> GetMapping();
		ExpressionToProperty GetIdMappingElement();
	}
}
