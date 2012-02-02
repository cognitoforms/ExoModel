using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Objects;
using System.Data.EntityClient;

namespace ExoModel.EntityFramework
{
	public class ModelObjectContext : ObjectContext, IEntityContext
	{
    	/// <summary>
    	/// Initialize a new ModelObjectContext object.
    	/// </summary>
    	public ModelObjectContext(string connectionString, string defaultContainerName) : base(connectionString, defaultContainerName)
		{ }
    
    	/// <summary>
    	/// Initialize a new ModelObjectContext object.
    	/// </summary>
		public ModelObjectContext(EntityConnection connection, string defaultContainerName)
			: base(connection, defaultContainerName)
    	{ }

		public override int SaveChanges(SaveOptions options)
		{
			int result = base.SaveChanges(options);

			if (SavedChanges != null)
				SavedChanges(this, new EventArgs());

			return result;
		}

		public event EventHandler SavedChanges;

		ObjectContext IEntityContext.ObjectContext { get { return this; } }

		void IEntityContext.OnSavedChanges()
		{
			if (SavedChanges != null)
				SavedChanges(this, new EventArgs());
		}
	}
}
