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
	/// <summary>
	/// Loads an XLSX file into memory and exposes each worksheet as a table of string values.
	/// </summary>
	public class ExcelFile : ITabularImportFile, IDisposable
	{
		SpreadsheetDocument spreadsheetDocument;
		SharedStringTable sharedStrings;
		Workbook workbook;

		/// <summary>
		/// Create a new <see cref="ExcelFile"/> from the specified stream.
		/// </summary>
		/// <param name="file">The excel file as a binary stream.</param>
		public ExcelFile(Stream file)
		{
			this.spreadsheetDocument = SpreadsheetDocument.Open(file, false);
			this.sharedStrings = spreadsheetDocument.WorkbookPart.SharedStringTablePart.SharedStringTable;
			this.workbook = spreadsheetDocument.WorkbookPart.Workbook;
		}

		/// <summary>
		/// Returns a list of all the types generated from the Excel file.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<string> GetTableNames()
		{
			return workbook.Descendants<Sheet>().Select(s => s.Name.Value);
		}

		/// <summary>
		/// Gets the table names for the current data set.
		/// </summary>
		/// <returns></returns>
		IEnumerable<string> ITabularImportFile.GetTableNames()
		{
			return workbook.Descendants<Sheet>().Select(s => s.Name.Value);
		}

		/// <summary>
		/// Gets the column names for the specified table.
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		IEnumerable<string> ITabularImportFile.GetColumnNames(string table)
		{
			var firstRow = GetWorksheet(table).Descendants<Row>().First();

			uint column = 1;
			foreach (var cell in firstRow.Descendants<Cell>())
			{
				if (cell.CellReference.Value == GetCellReference(column++, 1))
					yield return GetCellValue(cell);
				else
					break;
			}
		}

		/// <summary>
		/// Gets the row data for the specified table.
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		IEnumerable<string[]> ITabularImportFile.GetRows(string table)
		{
			// Determine the number of columns
			int columns = ((ITabularImportFile)this).GetColumnNames(table).Count();

			// Read each row
			foreach (var row in GetWorksheet(table).Descendants<Row>().Skip(1))
			{
				var values = new string[columns];
				uint column = 0;
				foreach (var cell in row.Descendants<Cell>())
				{
					while (column < columns && cell.CellReference.Value != GetCellReference(column + 1, row.RowIndex.Value))
						values[column++] = "";
					if (column < columns)
						values[column++] = GetCellValue(cell);
					if (column >= columns)
						break;
				}
				while (column < columns)
					values[column++] = "";
				yield return values;
			}
		}

		/// <summary>
		/// Gets the worksheet with the specified name.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		Worksheet GetWorksheet(string name)
		{
			var sheet = workbook.Descendants<Sheet>().FirstOrDefault(s => s.Name.Value == name);
			if (sheet == null)
				return null;

			return ((WorksheetPart)spreadsheetDocument.WorkbookPart.GetPartById(sheet.Id)).Worksheet;
		}

		/// <summary>
		/// Gets the text value of the specified cell.
		/// </summary>
		/// <param name="cell"></param>
		/// <returns></returns>
		string GetCellValue(Cell cell)
		{
			// Shared String
			if (cell.DataType != null && cell.DataType.HasValue && cell.DataType == CellValues.SharedString)
				return sharedStrings.ChildElements[int.Parse(cell.CellValue.InnerText)].InnerText;
			
			// Number
			double d;
			if (Double.TryParse(cell.CellValue.InnerText, out d) && cell.StyleIndex != null)
			{
				var format = spreadsheetDocument.WorkbookPart.WorkbookStylesPart.Stylesheet.CellFormats.ChildElements[int.Parse(cell.StyleIndex.InnerText)] as CellFormat;
				if (format.NumberFormatId >= 14 && format.NumberFormatId <= 22)
					return DateTime.FromOADate(d).ToString("G");
				else
					return d.ToString();
			}
			else
				return cell.CellValue.InnerText;
		}

		/// <summary>
		/// Gets the cell reference for the specified column and row indexes.
		/// </summary>
		/// <param name="column"></param>
		/// <param name="row"></param>
		/// <returns></returns>
		string GetCellReference(uint column, uint row)
		{
			return column <= 26 ? 
				(char)('A' + column - 1) + row.ToString() : 
				(char)('A' + (column / 26) - 1) + (char)('A' + (column % 26) - 1) + row.ToString();
		}

		void IDisposable.Dispose()
		{
			spreadsheetDocument.Close();
		}
	}
}
