using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel.ETL
{
	/// <summary>
	/// Simple read-write memory-based implementation of <see cref="ITable"/>.
	/// </summary>
	public class TableMapping
	{
		public string Identifier { get; set; }

		public string ParentIdentifier { get; set; }

		public string Name { get; set; }

		public IEnumerable<TableMapping> Children { get; set; }
	}
}
