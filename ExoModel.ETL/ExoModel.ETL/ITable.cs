using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.ETL
{
	/// <summary>
	/// Represents a string-based table within a hierarchial data set.
	/// </summary>
	public interface ITable : IDisposable
	{
		string Identifier { get; }

		string ParentIdentifier { get; }

		string Name { get; }

		ITable Parent { get; }

		IEnumerable<ITable> Children { get; }

		IEnumerable<Column> Columns { get; }

		IEnumerable<IEnumerable<string>> Rows { get; }
	}
}
