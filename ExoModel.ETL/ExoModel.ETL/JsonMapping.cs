using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace ExoModel.ETL
{
	public class JsonMapping : AbstractMapping
	{
		public JsonMapping(string jsonString)
		{
			Mapping = new List<ExpressionToProperty>();

			//build the Mapper object based on the jsonString and the deserializable type
			if (jsonString != null)
			{
				JavaScriptSerializer serializer = new JavaScriptSerializer();
				Mapping = serializer.Deserialize<IList<ExpressionToProperty>>(jsonString);
			}
		}

		public JsonMapping(IList<ExpressionToProperty> map)
		{
			Mapping = map;
		}
	}
}
