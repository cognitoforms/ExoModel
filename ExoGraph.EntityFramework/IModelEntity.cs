using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Objects.DataClasses;

namespace ExoModel.EntityFramework
{
	/// <summary>
	/// Interface implemented by all ExoModel aware Entity Framework entities.
	/// </summary>
	public interface IModelEntity : IModelInstance, IEntityWithKey, IEntityWithRelationships, IEntityWithChangeTracker
	{
		IEntityChangeTracker ChangeTracker { get; set; }

		bool IsInitialized { get; set; }
	}
}
