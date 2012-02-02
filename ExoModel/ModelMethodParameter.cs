using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	public class ModelMethodParameter
	{
		#region Constructors

		protected ModelMethodParameter(ModelMethod method, string name, Type valueType)
		{
			this.Method = method;
			this.Name = name;
			this.ValueType = valueType;
		}

		protected ModelMethodParameter(ModelMethod method, string name, ModelType referenceType, bool isList)
		{
			this.Method = method;
			this.Name = name;
			this.ReferenceType = referenceType;
			this.IsList = isList;
		}

		#endregion

		#region Properties

		public ModelMethod Method { get; private set; }

		public string Name { get; private set; }

		public ModelType ReferenceType { get; private set; }

		public Type ValueType { get; private set; }

		public bool IsList { get; private set; }

		public int Index { get; internal set; }

		#endregion
	}
}
