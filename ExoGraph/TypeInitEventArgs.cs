using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	public class TypeInitEventArgs : EventArgs
	{
		ModelType[] modelTypes;

		public ModelType[] ModelTypes
		{
			get
			{
				return modelTypes;
			}
		}

		public TypeInitEventArgs()
		{
		}

		public TypeInitEventArgs(ModelType[] types)
		{
			modelTypes = types;
		}
	}
}
