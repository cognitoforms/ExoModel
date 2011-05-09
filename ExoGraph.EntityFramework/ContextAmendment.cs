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
		public override void Amend()
		{
			var savedChanges = new Event<EventHandler>("SavedChanges");

			// IEntityContext
			ImplementInterface<IEntityContext>(
				new Property<ObjectContext>("ObjectContext") { Getter = EntityAdapter.GetObjectContext },
				savedChanges,
				savedChanges.RaisedBy("OnSavedChanges")
			);

			// Override SaveChanges
			AddMethod(Method.Override("SaveChanges").After((Method.AfterMethodFunc<int>)EntityAdapter.AfterSaveChanges));
		}
	}
}

