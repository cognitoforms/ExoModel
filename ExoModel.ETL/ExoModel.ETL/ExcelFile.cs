using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;
using System.IO;
using System.Data;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ExoModel.ETL
{
	public class ExcelFile : IImportFile
	{
		private IRowTypeProvider provider;
		private Dictionary<ModelType, string> generatedTypes;
		DataSet fileResults;

		/// <summary>
		/// Using the byte data build a representation of the Excel file.
		/// </summary>
		/// <param name="fileData">The excel file as a byte array.</param>
		/// <param name="isXmlFormat">Whether or not the Excel file is 2007 format or greater.</param>
		/// <param name="provider">The type provider to create the Types from the Excel file.</param>
		public ExcelFile(Stream file, IRowTypeProvider provider)
		{
			this.provider = provider;
			this.fileResults = new DataSet();
			generatedTypes = new Dictionary<ModelType, string>();

			using (SpreadsheetDocument spreadsheetDocument = SpreadsheetDocument.Open(file, false))
			{
				var sharedStrings = spreadsheetDocument.WorkbookPart.SharedStringTablePart.SharedStringTable;
				Workbook book = spreadsheetDocument.WorkbookPart.Workbook;

				foreach (Sheet sheet in book.Descendants<Sheet>())
				{
					var worksheet = (WorksheetPart)spreadsheetDocument.WorkbookPart.GetPartById(sheet.Id);

					foreach (Row row in worksheet.Worksheet.Descendants<Row>())
					{
						IEnumerable<String> textValues =
							from cell in row.Descendants<Cell>()
							where cell.CellValue != null
							select
							  (cell.DataType != null
								&& cell.DataType.HasValue
								&& cell.DataType == CellValues.SharedString
							  ? sharedStrings.ChildElements[
								int.Parse(cell.CellValue.InnerText)].InnerText
							  : 
								cell.CellValue.InnerText)
							;

						//create the type if if is the first column
						if (row.RowIndex == 1)
						{
							ModelType temp;
							provider.CreateType(textValues, out temp, sheet.Name);
							generatedTypes.Add(temp, sheet.Name);
							fileResults.Tables.Add(new DataTable(sheet.Name));
							foreach(string name in textValues)
							{
								fileResults.Tables[sheet.Name].Columns.Add(name, typeof(string));
							}
						}
						else
						{
							//add the row data
							DataTable dt = fileResults.Tables[sheet.Name];
							dt.Rows.Add(textValues.ToArray());
						}
					}
				}
			}
		}

		/// <summary>
		/// Returns a list of all the types generated from the Excel file.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<ModelType> GetTypesGenerated()
		{
			return generatedTypes.Keys;
		}

		/// <summary>
		/// This function will generate a ModelInstance of the
		/// dynamic type representative of the Excel file.
		/// </summary>
		/// <param name="type">The type of entities you want to retrieve from the file.</param>
		/// <returns></returns>
		public IEnumerable<ModelInstance> GetInstances(ModelType type)
		{
			//get the table name associated with this type
			string selectedTableName = generatedTypes[type];

			DataTable table = fileResults.Tables[selectedTableName];

			//now start generating instances
			foreach (DataRow row in table.Rows)
			{
				//first make sure this instance has not already been created 
				//in multiple calls to getinstances, some instances may have
				//already been loaded.
				ModelInstance testIsExists = provider.GetModelInstance(type, row.ItemArray[0].ToString());
				if(testIsExists == null)
				{
					provider.CreateInstance(type, row.ItemArray);
					testIsExists = provider.GetModelInstance(type, row.ItemArray[0].ToString());
				}

				yield return testIsExists;
			}
		}
	}
}
