using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoGraph
{
	public class TypesInitializedEventArgs : EventArgs
	{
		GraphType[] graphTypes;

		public GraphType[] GraphTypes
		{
			get
			{
				return graphTypes;
			}
		}

		public TypesInitializedEventArgs()
		{
		}

		public TypesInitializedEventArgs(GraphType[] types)
		{
			graphTypes = types;
		}
	}
}
