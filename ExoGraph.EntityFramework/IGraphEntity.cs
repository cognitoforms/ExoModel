using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Objects.DataClasses;

namespace ExoGraph.EntityFramework
{
	/// <summary>
	/// Interface implemented by all ExoGraph aware Entity Framework entities.
	/// </summary>
	public interface IGraphEntity : IGraphInstance, IEntityWithKey, IEntityWithRelationships, IEntityWithChangeTracker
	{
		IEntityChangeTracker ChangeTracker { get; set; }

		bool IsInitialized { get; set; }
	}
}
