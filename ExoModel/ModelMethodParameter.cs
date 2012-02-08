using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	public class ModelMethodParameter
	{
		#region Constructors

		protected ModelMethodParameter(ModelMethod method, string name, Type parameterType, ModelType referenceType, bool isList)
		{
			this.Method = method;
			this.Name = name;
			this.ParameterType = parameterType;
			this.ReferenceType = referenceType;
			this.IsList = isList;
		}

		#endregion

		#region Properties

		public ModelMethod Method { get; private set; }

		public string Name { get; private set; }

		public ModelType ReferenceType { get; private set; }

		public Type ParameterType { get; private set; }

		public bool IsList { get; private set; }

		public int Index { get; internal set; }

		#endregion
	}
}