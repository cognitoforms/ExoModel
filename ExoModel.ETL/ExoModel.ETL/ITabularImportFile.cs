using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.ETL
{
	public interface ITabularImportFile : IDisposable
	{
		IEnumerable<string> GetTableNames();
		IEnumerable<string> GetColumnNames(string table);
		IEnumerable<string[]> GetRows(string table);
	}
}
