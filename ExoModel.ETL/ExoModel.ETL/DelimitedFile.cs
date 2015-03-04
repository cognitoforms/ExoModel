using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;
using System.IO;
using System.Data;

namespace ExoModel.ETL
{
	/// <summary>
	/// Reads and writes sets of delimited files representing hierarchially related tabular data.
	/// </summary>
	public static class DelimitedFile
	{
		/// <summary>
		/// Reads an <see cref="ExcelFile"/> from the specified stream as an <see cref="ITable"/> instance.
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		public static ITable Read(params FileStream[] files)
		{
			return Read(',', '"', files);
		}

		/// <summary>
		/// Reads an <see cref="ExcelFile"/> from the specified stream as an <see cref="ITable"/> instance.
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		public static ITable Read(char delimiter = ',', char qualifier = '"', params FileStream[] files)
		{
			var table = new Table(files[0], Path.GetFileNameWithoutExtension(files[0].FileName), null, delimiter, qualifier);
			ReadChildren(table, files, table.Name, delimiter, qualifier);
			return table;
		}

		/// <summary>
		/// Recursively reads child tables to build the table graph.
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="file"></param>
		/// <param name="sheets"></param>
		/// <param name="path"></param>
		static void ReadChildren(Table parent, FileStream[] files, string path, char delimiter, char qualifier)
		{
			path += ".";
			foreach (var file in files)
			{
				var name = Path.GetFileNameWithoutExtension(file.FileName);
				if (name.StartsWith(path, StringComparison.InvariantCultureIgnoreCase) && name.Length < path.Length && name.IndexOf('.', path.Length) < 0)
				{
					var child = new Table(file, name.Substring(path.Length), parent, delimiter, qualifier);
					ReadChildren(child, files, path + child.Name, delimiter, qualifier);
				}
			}
		}

		/// <summary>
		/// Writes an <see cref="ITable"/> instance as an <see cref="ExcelFile"/> to the specified stream.
		/// </summary>
		/// <param name="table"></param>
		/// <param name="file"></param>
		public static void Write(ITable table, Stream file)
		{
		}

		/// <summary>
		/// Exposes a delimited file as an <see cref="ITable"/> instance.
		/// </summary>
		class Table : ITable
		{
			FileStream file;
			char delimiter;
			char qualifier;
			List<ITable> children = new List<ITable>();

			/// <summary>
			/// Creates a new <see cref="Table"/> from the specified file.
			/// </summary>
			/// <param name="file"></param>
			/// <param name="name"></param>
			/// <param name="parent"></param>
			/// <param name="delimiter"></param>
			/// <param name="qualifier"></param>
			internal Table(FileStream file, string name, Table parent, char delimiter = ',', char qualifier = '"')
				: base()
			{
				this.Name = name;
				this.file = file;
				this.Parent = parent;
				this.delimiter = delimiter;
				this.qualifier = qualifier;
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
				get { return EnumerateRows().First().Select(c => new Column(c)); }
			}

			/// <summary>
			/// Get the data for each row in the current table.
			/// </summary>
			public IEnumerable<IEnumerable<string>> Rows
			{
				get { return EnumerateRows().Skip(1); }
			}

			/// <summary>
			/// Enumerates the rows in the underlying delimited file.
			/// </summary>
			/// <returns></returns>
			IEnumerable<IEnumerable<string>> EnumerateRows()
			{
				// Reset the stream
				file.InputStream.Seek(0, SeekOrigin.Begin);

				IList<string> result = new List<string>();

				var value = new StringBuilder();

				var stream = new StreamReader(file.InputStream, Encoding.Unicode);

				char c;
				c = (char)stream.Read();
				while (stream.Peek() != -1)
				{
					if (c == delimiter)
					{
						result.Add("");
						c = (char)stream.Read();
					}
					else if (c == qualifier)
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
						if (c == delimiter) c = (char)stream.Read(); // either ',', newline, or endofstream
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
						while (c != delimiter && c != '\r' && c != '\n')
						{
							value.Append(c);

							// if there is no character to read, break out of while loop
							if (stream.Peek() == -1)
								break;

							c = (char)stream.Read();
						}
						result.Add(value.ToString());
						value.Clear();
						if (c == delimiter) c = (char)stream.Read();
					}
				}
				if (value.Length > 0) //potential bug: I don't want to skip on a empty column in the last record if a caller really expects it to be there
					result.Add(value.ToString());
				if (result.Count > 0)
					yield return result;
			}

			/// <summary>
			/// Disposes the table by closing the underlying file stream.
			/// </summary>
			public void Dispose()
			{
				file.InputStream.Dispose();
				foreach (var child in children)
					child.Dispose();
			}
		}
	}
}