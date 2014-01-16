using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel.ETL
{
	public class Column
	{
		public Column(string name, Type type = null, string format = null)
		{
			this.Name = name;
			this.Type = type ?? typeof(string);
			this.Format = format;
		}

		public string Name { get; private set; }

		public Type Type { get; private set; }

		public string Format { get; private set; }
	}
}
