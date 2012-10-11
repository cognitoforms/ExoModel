using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;
using System.IO;
using System.Data;

namespace ExoModel.ETL
{
	public class DelimitedFile : IDisposable
	{
		public string Name { get; set; }
		public Stream File { get; set; }
		public char Delimiter { get; set; }
		public char Qualifier { get; set; }

		public static DelimitedFile Csv(string name, Stream file)
		{
			return new DelimitedFile { Name = name, File = file, Delimiter = ',', Qualifier = '"' };
		}

		void IDisposable.Dispose()
		{
			File.Dispose();
		}
	}

	/// <summary>
	/// Loads an XLSX file into memory and exposes each worksheet as a table of string values.
	/// </summary>
	public class DelimitedFileSet : ITabularImportFile
	{
		Dictionary<string, DelimitedFile> files;

		/// <summary>
		/// Create a new <see cref="CsvFile"/> from the specified stream.
		/// </summary>
		/// <param name="file">The CSV file as a binary stream.</param>
		public DelimitedFileSet(params DelimitedFile[] files)
		{
			this.files = files.ToDictionary(f => f.Name);
		}

		/// <summary>
		/// Gets the table names for the current data set.
		/// </summary>
		/// <returns></returns>
		IEnumerable<string> ITabularImportFile.GetTableNames()
		{
			return files.Keys;
		}

		/// <summary>
		/// Enumerates the rows in the <see cref="DelimitedFile"/>.
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		static IEnumerable<string[]> GetRows(DelimitedFile file)
		{
			// Reset the stream
			file.File.Seek(0, SeekOrigin.Begin);

			IList<string> result = new List<string>();

			var value = new StringBuilder();

			var stream = new StreamReader(file.File);

			char c;
			c = (char)stream.Read();
			while (stream.Peek() != -1)
			{
				if (c == file.Delimiter)
				{
					result.Add("");
					c = (char)stream.Read();
				}
				else if (c == file.Qualifier)
				{
					char q = c;
					c = (char)stream.Read();
					bool inQuotes = true;
					while (inQuotes && stream.Peek() != -1)
					{
						if (c == q)
						{
							c = (char)stream.Read();
							if (c != q)
								inQuotes = false;
						}
						if (inQuotes)
						{
							value.Append(c);
							c = (char)stream.Read();
						}
					}
					result.Add(value.ToString());
					value = new StringBuilder();
					if (c == file.Delimiter) c = (char)stream.Read(); // either ',', newline, or endofstream
				}
				else if (c == '\n' || c == '\r')
				{
					if (result.Count > 0)
					{
						yield return result.ToArray();
						result.Clear();
					}
					c = (char)stream.Read();
				}
				else
				{
					while (c != file.Delimiter && c != '\r' && c != '\n' && stream.Peek() != -1)
					{
						value.Append(c);
						c = (char)stream.Read();
					}
					result.Add(value.ToString());
					value.Clear();
					if (c == file.Delimiter) c = (char)stream.Read();
				}
			}
			if (value.Length > 0) //potential bug: I don't want to skip on a empty column in the last record if a caller really expects it to be there
				result.Add(value.ToString());
			if (result.Count > 0)
				yield return result.ToArray();
		}

		/// <summary>
		/// Gets the column names for the specified table.
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		IEnumerable<string> ITabularImportFile.GetColumnNames(string table)
		{
			return GetRows(files[table]).First();
		}

		/// <summary>
		/// Gets the row data for the specified table.
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		IEnumerable<string[]> ITabularImportFile.GetRows(string table)
		{
			return GetRows(files[table]).Skip(1);
		}

		/// <summary>
		/// Dispose of the file streams in the current file set.
		/// </summary>
		void IDisposable.Dispose()
		{
			foreach (IDisposable file in files.Values)
				file.Dispose();
		}
	}
}