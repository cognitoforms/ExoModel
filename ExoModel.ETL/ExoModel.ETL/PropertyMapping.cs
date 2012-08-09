using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel.ETL
{
	/// <summary>
	/// Represents the mapping of a source expression to a specific property on the destination.
	/// </summary>
	public class PropertyMapping
	{
		public string SourceExpression { get; set; }

		public string DestinationProperty { get; set; }
	}
}
