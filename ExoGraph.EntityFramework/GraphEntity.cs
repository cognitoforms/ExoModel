using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Objects.DataClasses;
using System.Data.Objects;

namespace ExoGraph.EntityFramework
{
	public abstract class GraphEntity : EntityObject, IGraphInstance
	{
		GraphInstance instance;

		public GraphEntity()
		{
			instance = new GraphInstance(this);
		}

		GraphInstance IGraphInstance.Instance
		{
			get
			{
				return instance;
			}
		}

		internal string Id
		{
			get
			{
				if (EntityKey == null || EntityKey.IsTemporary)
					return null;
				else if (EntityKey.EntityKeyValues.Length > 1)
					return EntityKey.EntityKeyValues.Select(v => v.Value.ToString()).Aggregate((v1, v2) => v1 + "," + v2);
				else
					return EntityKey.EntityKeyValues[0].Value.ToString();
			}
		}

		protected void OnPropertyGet(string property)
		{
			if (this.EntityState != System.Data.EntityState.Detached)
				instance.OnPropertyGet(property);
		}

		protected void OnPropertySet(string property, object oldValue, object newValue)
		{
			// Raise property change if the new value is different from the old value
			if (this.EntityState != System.Data.EntityState.Detached && ((oldValue == null ^ newValue == null) || (oldValue != null && !oldValue.Equals(newValue))))
				instance.OnPropertyChanged(property, oldValue, newValue);
		}
	}

	public abstract class GraphEntity<TObjectContext> : GraphEntity
		where TObjectContext : GraphObjectContext
	{
		/// <summary>
		/// Enables the base class to know the type of the concrete subclass in order to obtain the
		/// correct <see cref="GraphObjectContext"/>.  Ideally, this would be a generic type parameter,
		/// but a bug in the Entity Framework does not support curiously recursing generic types
		/// as a subclass of EntityObject.
		/// </summary>
		protected static Type entityType;

		protected static TObjectContext ObjectContext
		{
			get
			{
				return (TObjectContext)((EntityFrameworkGraphTypeProvider.EntityGraphType)GraphContext.Current.GetGraphType(entityType)).GetObjectContext();
			}
		}


	}
}
