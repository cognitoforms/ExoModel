using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace ExoModel.ETL
{
	public class DataTableBasedFile : ITabularImportFile
	{
		private DataSet Data { get; set; }

		public DataTableBasedFile(DataSet data)
		{
			Data = data;
		}

		IEnumerable<string> ITabularImportFile.GetTableNames()
		{
			foreach (DataTable t in Data.Tables)
			{
				yield return t.TableName;
			}
		}

		IEnumerable<string> ITabularImportFile.GetColumnNames(string table)
		{
			foreach (DataTable t in Data.Tables)
			{
				if (t.TableName == table)
				{
					foreach (DataColumn col in t.Columns)
					{
						yield return col.ColumnName;
					}
				}
			}
		}

		IEnumerable<string[]> ITabularImportFile.GetRows(string table)
		{
			foreach (DataTable t in Data.Tables)
			{
				if (t.TableName == table)
				{
					foreach (DataRow r in t.Rows)
					{
						IList<string> temp = new List<string>();
						foreach (object item in r.ItemArray)
						{
							temp.Add(item.ToString());
						}
						yield return temp.ToArray();
					}
				}
			}
		}

		void IDisposable.Dispose()
		{
			//nothing to clean up
			return;
		}
	}
}
