using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Objects;

namespace ExoModel.EntityFramework
{
	public interface IEntityContext
	{
		event EventHandler SavedChanges;

		ObjectContext ObjectContext { get; }

		void OnSavedChanges();

		int SaveChanges();
	}
}
