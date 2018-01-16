using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel.ETL
{
	/// <summary>
	/// Interface for objects that have <see cref="Column"/> characteristics.
	/// </summary>
	public interface IColumn
	{
		string Label { get; }

		Type PropertyType { get; }

		string Format { get; }

		int Sequence { get; }

		double? Width { get; }
	}
}
