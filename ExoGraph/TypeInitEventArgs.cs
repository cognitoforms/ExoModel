using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoGraph
{
	public class TypeInitEventArgs : EventArgs
	{
		GraphType[] graphTypes;

		public GraphType[] GraphTypes
		{
			get
			{
				return graphTypes;
			}
		}

		public TypeInitEventArgs()
		{
		}

		public TypeInitEventArgs(GraphType[] types)
		{
			graphTypes = types;
		}
	}
}
