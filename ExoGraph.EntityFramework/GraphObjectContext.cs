using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Objects;
using System.Data.EntityClient;

namespace ExoGraph.EntityFramework
{
	public class GraphObjectContext : ObjectContext
	{
		public event EventHandler SavedChanges;

    	/// <summary>
    	/// Initialize a new GraphObjectContext object.
    	/// </summary>
    	public GraphObjectContext(string connectionString, string defaultContainerName) : base(connectionString, defaultContainerName)
		{ }
    
    	/// <summary>
    	/// Initialize a new GraphObjectContext object.
    	/// </summary>
    	public GraphObjectContext(EntityConnection connection, string defaultContainerName) : base(connection, defaultContainerName)
    	{ }

		public override int SaveChanges(SaveOptions options)
		{
			int result = base.SaveChanges(options);

			if (SavedChanges != null)
				SavedChanges(this, new EventArgs());

			return result;
		}
	}
}
