using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Objects;

namespace ExoGraph.EntityFramework
{
	public interface IEntityContext
	{
		event EventHandler SavedChanges;

		ObjectContext ObjectContext { get; }
	}
}
