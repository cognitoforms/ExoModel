using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afterthought;
using System.Data.Objects.DataClasses;
using System.Reflection;
using System.Data.Metadata.Edm;
using System.Data.Objects;

namespace ExoGraph.EntityFramework
{
	/// <summary>
	/// Amends types after compilation to support Entity Framework and ExoGraph.
	/// </summary>
	/// <typeparam name="TType"></typeparam>
	public class ContextAmendment<TType> : Amendment<TType, IEntityContext>
	{
		/// <summary>
		/// Amend the type to implement <see cref="IEntityContext"/>.
		/// </summary>
		public ContextAmendment()
		{
			var savedChanges = Events.Add<EventHandler>("SavedChanges");

			// IEntityContext
			Implement<IEntityContext>(
				Properties.Add<ObjectContext>("ObjectContext").Get(EntityAdapter.GetObjectContext),
				savedChanges,
				Methods.Raise(savedChanges, "OnSavedChanges")
			);

			// Override SaveChanges
			Methods.Override("SaveChanges").After<int>(EntityAdapter.AfterSaveChanges);
		}
	}
}

