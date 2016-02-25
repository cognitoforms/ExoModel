using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExoModel;
using System.IO;
using System.Data;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml;
using System.Globalization;
using Ap = DocumentFormat.OpenXml.ExtendedProperties;
using Vt = DocumentFormat.OpenXml.VariantTypes;
using X15ac = DocumentFormat.OpenXml.Office2013.ExcelAc;
using X15 = DocumentFormat.OpenXml.Office2013.Excel;
using X14 = DocumentFormat.OpenXml.Office2010.Excel;
using A = DocumentFormat.OpenXml.Drawing;
using Thm15 = DocumentFormat.OpenXml.Office2013.Theme;

namespace ExoModel.ETL
{
	/// <summary>
	/// Loads an XLSX file into memory and exposes each worksheet as a table of string values.
	/// </summary>
	public class ExcelFile : IDisposable
	{
		SpreadsheetDocument spreadsheet;
		SharedStringTable sharedStrings;
		Workbook workbook;

		/// <summary>
		/// Reads an <see cref="ExcelFile"/> from the specified stream as an <see cref="ITable"/> instance.
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		public static ITable Read(Stream file, TableMapping mapping = null)
		{
			var excelFile = new ExcelFile(file);
			
			// Infer or load mappings from the spreadsheet if they are not specified explicitly
			if (mapping == null)
				throw new NotImplementedException("Add support for inferring the mappings for single sheet imports or loading the mappings from a manifest sheet.");

			return new Table(excelFile, mapping, null);
		}

		/// <summary>
		/// Writes an <see cref="ITable"/> instance as an <see cref="ExcelFile"/> to the specified stream.
		/// </summary>
		/// <param name="table"></param>
		/// <param name="file"></param>
		public static void Write(ITable table, Stream file, TimeZoneInfo timezone = null)
		{
			var document = Template.CreateDocument(file);
			//Sheets sheets = document.WorkbookPart.Workbook.AppendChild<Sheets>(new Sheets());

			Write(table, document, timezone);

			document.WorkbookPart.Workbook.Save();
			document.Close();
		}

		/// <summary>
		/// Writes an <see cref="ITable"/> instance as an <see cref="ExcelFile"/> to the specified stream.
		/// </summary>
		/// <param name="table"></param>
		/// <param name="file"></param>
		public static void Write(ITable table, SpreadsheetDocument document, TimeZoneInfo timezone = null)
		{
			var worksheetPart = document.WorkbookPart.AddNewPart<WorksheetPart>();
			worksheetPart.Worksheet = new Worksheet(new SheetData());
			Sheet sheet = new Sheet()
			{
				Id = document.WorkbookPart.GetIdOfPart(worksheetPart),
				SheetId = (uint)document.WorkbookPart.Workbook.Sheets.Count() + 1,
				Name = table.Name
			};
			document.WorkbookPart.Workbook.Sheets.Append(sheet);

			// Get the sheetData cell table.
			var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

			// Add a row to the cell table.
			Row row;
			row = new Row() { RowIndex = 1 };
			sheetData.Append(row);

			// Add cells for each column header and create styles for each column
			var index = 0;
			var formats = new List<ColumnFormat>();
			foreach (var column in table.Columns)
			{
				// Add the cell to the cell table at A1.
				var cell = new Cell()
				{
					CellReference = GetCellReference((uint)index + 1, 1),
					CellValue = new CellValue(column.Name),
					DataType = new EnumValue<CellValues>(CellValues.String)
				};
				row.InsertAt(cell, index++);
				formats.Add(new ColumnFormat(column, document, timezone));
			}

			// Add rows for each table entity
			uint rowIndex = 2;
			foreach (var tableRow in table.Rows)
			{
				row = new Row() { RowIndex = rowIndex };
				sheetData.Append(row);
				var columnIndex = 0;
				foreach (var value in tableRow)
				{
					var format = formats[columnIndex];

					// Add the cell to the cell table at A1.
					var cell = new Cell()
					{
						CellReference = GetCellReference((uint)columnIndex + 1, rowIndex),
						CellValue = new CellValue(format.GetCellValue(value)),
						DataType = format.DataType,
						StyleIndex = format.Style
					};
					row.InsertAt(cell, columnIndex);
					columnIndex++;
				}
				rowIndex++;
			}

			// Recursively write child tables
			foreach (var child in table.Children)
				Write(child, document);
		}

		// Describes the format of a column in excel
		class ColumnFormat
		{
			private static readonly Regex TextFilter = new Regex(@"[^\x09\x0A\x0D\x20-\uD7FF\uE000-\uFFFD\u10000-\u10FFFF]", RegexOptions.Compiled | RegexOptions.Multiline);

			internal ColumnFormat(Column column, SpreadsheetDocument document, TimeZoneInfo timezone = null)
			{
				this.Column = column;
				var type = column.Type;
				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
					type = type.GetGenericArguments()[0];
				if (type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(float) || type == typeof(double))
				{
					DataType = new EnumValue<CellValues>(CellValues.Number);
					if (!String.IsNullOrWhiteSpace(column.Format))
					{
						var percentSymbol = NumberFormatInfo.CurrentInfo.PercentSymbol;
						if (column.Format == "P" || column.Format == "p" || column.Format.Contains(NumberFormatInfo.CurrentInfo.PercentSymbol))
						{
							Style = CreateStyle(11.10.ToString(column.Format, CultureInfo.InvariantCulture).Replace("1", "#") + ";" + (-11.10).ToString(column.Format, CultureInfo.InvariantCulture).Replace("1", "#"), document);
							GetCellValue = v => String.IsNullOrWhiteSpace(v) ? v : (Double.Parse(v.Replace(NumberFormatInfo.CurrentInfo.PercentSymbol, ""), NumberStyles.Any) / 100).ToString(CultureInfo.InvariantCulture);
						}
						else if (column.Format == "C" || column.Format == "c" || column.Format.Contains(NumberFormatInfo.CurrentInfo.CurrencySymbol))
						{
							// get the culture specific pattern
							var pattern = 1110.00.ToString("C").Replace("1", "#").Replace(NumberFormatInfo.CurrentInfo.CurrencySymbol, "[$" + NumberFormatInfo.CurrentInfo.CurrencySymbol + "-1]");
							// convert everything but the currency symbol and placement to the invariant pattern
							pattern = new System.Text.RegularExpressions.Regex("#.##0.00").Replace(pattern, "#,##0.00");

							// create the excel style
							Style = CreateStyle(pattern + ";(" + pattern + ")", document);
						}
						else
							Style = CreateStyle(1111.10.ToString(column.Format, CultureInfo.InvariantCulture).Replace("1", "#") + ";" + (-1111.10).ToString(column.Format, CultureInfo.InvariantCulture).Replace("1", "#") + ";" + 0.ToString(column.Format, CultureInfo.InvariantCulture), document);
					}
						
					if (GetCellValue == null)
						GetCellValue = v => String.IsNullOrWhiteSpace(v) ? v : Double.Parse(v, NumberStyles.Any).ToString(CultureInfo.InvariantCulture);
				}
				else
					if (type == typeof(DateTime) || type == typeof(Nullable<DateTime>))
					{
						var isDateTime = true;
						
						DataType = new EnumValue<CellValues>(CellValues.Number);
						if (!String.IsNullOrWhiteSpace(column.Format) && column.Format.Length == 1)
						{
							isDateTime = column.Format.ToLower() != "d" && column.Format.ToLower() != "t";

							var patterns = DateTimeFormatInfo.CurrentInfo.GetAllDateTimePatterns(column.Format[0]);
							if (patterns.Any())
								Style = CreateStyle(patterns[0].Replace("tt", "AM/PM"), document);
						}

						// adjust the time to match the specified timezone if the type is a true DateTime and not a Date only or Time only field
						if (timezone != null && isDateTime)
							GetCellValue = (v) =>
							{
								return String.IsNullOrWhiteSpace(v) ? v : TimeZoneInfo.ConvertTime(DateTime.Parse(v, null, DateTimeStyles.AssumeUniversal), timezone).ToOADate().ToString(CultureInfo.InvariantCulture);
							};
						else
							GetCellValue = v => String.IsNullOrWhiteSpace(v) ? v : DateTime.Parse(v).ToOADate().ToString(CultureInfo.InvariantCulture);
					}
					else
					{
						DataType = new EnumValue<CellValues>(CellValues.String);

						// Cleanse string based on acceptable xml character set
						// http://stackoverflow.com/questions/6468783/what-is-the-difference-between-cellvalues-inlinestring-and-cellvalues-string-in
						GetCellValue = v => TextFilter.Replace(v, "");
					}
			}

			internal Column Column { get; private set; }

			internal EnumValue<CellValues> DataType { get; private set; }

			internal uint Style { get; private set; }

			internal Func<string, string> GetCellValue { get; private set; }

			///<summary>
			///Returns an existing reserved number format style index or creates a new style index from the number format id provided.
			///</summary>
			public static uint CreateStyle(string format, SpreadsheetDocument document)
			{
				var styles = document.WorkbookPart.WorkbookStylesPart ?? document.WorkbookPart.AddNewPart<WorkbookStylesPart>();
				var stylesheet = styles.Stylesheet ?? (styles.Stylesheet = new Stylesheet());

				// Create the number format
				var numberFormats = stylesheet.NumberingFormats ?? (stylesheet.NumberingFormats = new NumberingFormats() { Count = 0 });
				NumberingFormat nf = new NumberingFormat() 
				{ 
					FormatCode = format,
					NumberFormatId = numberFormats.Count + (uint)164
				};
				numberFormats.AppendChild<NumberingFormat>(nf);
				numberFormats.Count++;

				// Create the cell format
				var cellFormats = stylesheet.CellFormats ?? (stylesheet.CellFormats = new CellFormats() { Count = 0 });
				CellFormat cf = new CellFormat()
				{
					FontId = 0,
					ApplyFont = false,
					BorderId = 0,
					ApplyBorder = false,
					FillId = 0,
					ApplyFill = false,
					FormatId = 0,
					NumberFormatId = nf.NumberFormatId,
					ApplyNumberFormat = true
				};
				cellFormats.AppendChild<CellFormat>(cf);
				return cellFormats.Count++;
			}
		}

		/// <summary>
		/// Exposes a worksheet in an <see cref="ExcelFile"/> as an <see cref="ITable"/> instance.
		/// </summary>
		class Table : ITable
		{
			ExcelFile file;
			Worksheet worksheet;
			List<ITable> children = new List<ITable>();

			internal Table(ExcelFile file, TableMapping mapping, Table parent)
			{
				this.file = file;
				this.Name = mapping.Name;
				this.Identifier = mapping.Identifier;
				this.Parent = parent;
				parent.children.Add(this);
				this.ParentIdentifier = mapping.ParentIdentifier;

				// Find the specified worksheet
				var sheet = file.workbook.Descendants<Sheet>().FirstOrDefault(s => s.Name.Value == mapping.Name);
				if (sheet == null)
					throw new ArgumentException("A worksheet named '" + mapping.Name + "' was not found.");
				this.worksheet = ((WorksheetPart)file.spreadsheet.WorkbookPart.GetPartById(sheet.Id)).Worksheet;

				// Load child tables
				foreach (var child in mapping.Children)
					children.Add(new Table(file, child, this));
			}

			/// <summary>
			/// Gets the name of the table.
			/// </summary>
			public string Name { get; private set; }

			/// <summary>
			/// Gets the identifier expression for the table.
			/// </summary>
			public string Identifier { get; private set; }

			/// <summary>
			/// Gets the parent <see cref="ITable"/>, or null for the root table.
			/// </summary>
			public ITable Parent { get; private set; }

			/// <summary>
			/// Gets the parent identifier expression for the table.
			/// </summary>
			public string ParentIdentifier { get; private set; }

			/// <summary>
			/// Gets the child <see cref="ITable"/>s representing lists of dependent data.
			/// </summary>
			public IEnumerable<ITable> Children
			{
				get { return children; }
			}

			/// <summary>
			/// Gets the column names for the current table.
			/// </summary>
			public IEnumerable<Column> Columns
			{
				get
				{
					var firstRow = worksheet.Descendants<Row>().First();

					uint column = 1;
					foreach (var cell in firstRow.Descendants<Cell>())
					{
						if (cell.CellReference.Value == GetCellReference(column++, 1))
							yield return new Column(GetCellValue(cell));
						else
							break;
					}
				}
			}

			/// <summary>
			/// Get the data for each row in the current table.
			/// </summary>
			public IEnumerable<IEnumerable<string>> Rows
			{
				get
				{
					// Determine the number of columns
					int columns = Columns.Count();

					// Read each row
					foreach (var row in worksheet.Descendants<Row>().Skip(1))
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
			}

			/// <summary>
			/// Disposes the table by closing the underlying spreadsheet.
			/// </summary>
			void IDisposable.Dispose()
			{
				((IDisposable)file).Dispose();
			}

			/// <summary>
			/// Gets the text value of the specified cell.
			/// </summary>
			/// <param name="cell"></param>
			/// <returns></returns>
			string GetCellValue(Cell cell)
			{
				if (cell.CellValue != null)
				{
					// Shared String
					if (cell.DataType != null && cell.DataType.HasValue && cell.DataType == CellValues.SharedString)
						return file.sharedStrings.ChildElements[int.Parse(cell.CellValue.InnerText)].InnerText;

					// Number
					double d;
					if (Double.TryParse(cell.CellValue.InnerText, out d) && cell.StyleIndex != null)
					{
						var format = file.spreadsheet.WorkbookPart.WorkbookStylesPart.Stylesheet.CellFormats.ChildElements[int.Parse(cell.StyleIndex.InnerText)] as CellFormat;
						if (format.NumberFormatId >= 14 && format.NumberFormatId <= 22)
							return DateTime.FromOADate(d).ToString("G");
						else
							return d.ToString();
					}
					else
						return cell.CellValue.InnerText;
				}
				else
				{
					return null;
				}
			}


		}

		ExcelFile()
		{ }

		/// <summary>
		/// Create a new <see cref="ExcelFile"/> from the specified stream.
		/// </summary>
		/// <param name="file">The excel file as a binary stream.</param>
		ExcelFile(Stream file)
		{
			this.spreadsheet = SpreadsheetDocument.Open(file, false);
			this.sharedStrings = spreadsheet.WorkbookPart.SharedStringTablePart.SharedStringTable;
			this.workbook = spreadsheet.WorkbookPart.Workbook;
		}

		/// <summary>
		/// Gets the cell reference for the specified column and row indexes.
		/// </summary>
		/// <param name="column"></param>
		/// <param name="row"></param>
		/// <returns></returns>
		static string GetCellReference(uint column, uint row)
		{
			return column <= 26 ?
				(char)('A' + column - 1) + row.ToString() :
				((char)('A' + ((column - 1) / 26) - 1)).ToString() + ((char)('A' + ((column - 1) % 26))).ToString() + row.ToString();
		}

		void IDisposable.Dispose()
		{
			spreadsheet.Close();
		}

		internal class Template
		{
			internal static SpreadsheetDocument CreateDocument(Stream stream)
			{
				return new Template().Create(stream);
			}

			// Creates a SpreadsheetDocument.
			public SpreadsheetDocument Create(Stream stream)
			{
				var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook);
				var workbookPart = document.AddWorkbookPart();
				workbookPart.Workbook = new Workbook();
				Sheets sheets = workbookPart.Workbook.AppendChild<Sheets>(new Sheets());

				WorkbookStylesPart workbookStylesPart1 = workbookPart.AddNewPart<WorkbookStylesPart>("rId3");
				GenerateWorkbookStylesPart1Content(workbookStylesPart1);

				return document;
			}

			// Generates content of workbookStylesPart1.
			private void GenerateWorkbookStylesPart1Content(WorkbookStylesPart workbookStylesPart1)
			{
				Stylesheet stylesheet1 = new Stylesheet() { MCAttributes = new MarkupCompatibilityAttributes() { Ignorable = "x14ac" } };
				stylesheet1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
				stylesheet1.AddNamespaceDeclaration("x14ac", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac");

				Fonts fonts1 = new Fonts() { Count = (UInt32Value)1U, KnownFonts = true };

				Font font1 = new Font();
				FontSize fontSize1 = new FontSize() { Val = 11D };
				Color color1 = new Color() { Theme = (UInt32Value)1U };
				FontName fontName1 = new FontName() { Val = "Calibri" };
				FontFamilyNumbering fontFamilyNumbering1 = new FontFamilyNumbering() { Val = 2 };
				FontScheme fontScheme1 = new FontScheme() { Val = FontSchemeValues.Minor };

				font1.Append(fontSize1);
				font1.Append(color1);
				font1.Append(fontName1);
				font1.Append(fontFamilyNumbering1);
				font1.Append(fontScheme1);

				fonts1.Append(font1);

				Fills fills1 = new Fills() { Count = (UInt32Value)2U };

				Fill fill1 = new Fill();
				PatternFill patternFill1 = new PatternFill() { PatternType = PatternValues.None };

				fill1.Append(patternFill1);

				Fill fill2 = new Fill();
				PatternFill patternFill2 = new PatternFill() { PatternType = PatternValues.Gray125 };

				fill2.Append(patternFill2);

				fills1.Append(fill1);
				fills1.Append(fill2);

				Borders borders1 = new Borders() { Count = (UInt32Value)1U };

				Border border1 = new Border();
				LeftBorder leftBorder1 = new LeftBorder();
				RightBorder rightBorder1 = new RightBorder();
				TopBorder topBorder1 = new TopBorder();
				BottomBorder bottomBorder1 = new BottomBorder();
				DiagonalBorder diagonalBorder1 = new DiagonalBorder();

				border1.Append(leftBorder1);
				border1.Append(rightBorder1);
				border1.Append(topBorder1);
				border1.Append(bottomBorder1);
				border1.Append(diagonalBorder1);

				borders1.Append(border1);

				CellStyleFormats cellStyleFormats1 = new CellStyleFormats() { Count = (UInt32Value)1U };
				CellFormat cellFormat1 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U };

				cellStyleFormats1.Append(cellFormat1);

				CellFormats cellFormats1 = new CellFormats() { Count = (UInt32Value)1U };
				CellFormat cellFormat2 = new CellFormat() { NumberFormatId = (UInt32Value)0U, FontId = (UInt32Value)0U, FillId = (UInt32Value)0U, BorderId = (UInt32Value)0U, FormatId = (UInt32Value)0U };

				cellFormats1.Append(cellFormat2);

				CellStyles cellStyles1 = new CellStyles() { Count = (UInt32Value)1U };
				CellStyle cellStyle1 = new CellStyle() { Name = "Normal", FormatId = (UInt32Value)0U, BuiltinId = (UInt32Value)0U };

				cellStyles1.Append(cellStyle1);
				DifferentialFormats differentialFormats1 = new DifferentialFormats() { Count = (UInt32Value)0U };
				TableStyles tableStyles1 = new TableStyles() { Count = (UInt32Value)0U, DefaultTableStyle = "TableStyleMedium2", DefaultPivotStyle = "PivotStyleLight16" };

				stylesheet1.Append(fonts1);
				stylesheet1.Append(fills1);
				stylesheet1.Append(borders1);
				stylesheet1.Append(cellStyleFormats1);
				stylesheet1.Append(cellFormats1);
				stylesheet1.Append(cellStyles1);
				stylesheet1.Append(differentialFormats1);
				stylesheet1.Append(tableStyles1);

				workbookStylesPart1.Stylesheet = stylesheet1;
			}
		}
	}
}
