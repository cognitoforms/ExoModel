using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoGraph
{
	public class GraphMethodParameter
	{
		#region Constructors

		protected GraphMethodParameter(GraphMethod method, string name, Type valueType)
		{
			this.Method = method;
			this.Name = name;
			this.ValueType = valueType;
		}

		protected GraphMethodParameter(GraphMethod method, string name, GraphType referenceType, bool isList)
		{
			this.Method = method;
			this.Name = name;
			this.ReferenceType = referenceType;
			this.IsList = isList;
		}

		#endregion

		#region Properties

		public GraphMethod Method { get; private set; }

		public string Name { get; private set; }

		public GraphType ReferenceType { get; private set; }

		public Type ValueType { get; private set; }

		public bool IsList { get; private set; }

		public int Index { get; internal set; }

		#endregion
	}
}
