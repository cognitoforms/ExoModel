using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Event;
using ExoGraph.Injection;

using IGraphInstance = ExoGraph.NHibernate.DataBindingFactory.IGraphInstance;

namespace ExoGraph.NHibernate
{
	/// <summary>
	/// An NHibernate event listener which fires when an entity has been persisted
	/// </summary>
	public class SaveNotificationEventListener : IPostUpdateEventListener, IPostInsertEventListener
	{
		public static readonly SaveNotificationEventListener Instance = new SaveNotificationEventListener();

		private SaveNotificationEventListener()
		{
		}

		private void RaiseOnSave(object entity)
		{
			if (entity is IGraphInstance)
				((NHibernateGraphTypeProvider.NHibernateGraphType) GraphContext.Current.GetGraphType(entity)).RaiseOnSave(((IGraphInstance) entity).Instance);
		}

		#region IPostUpdateEventListener Members

		public void OnPostUpdate(PostUpdateEvent @event)
		{
			RaiseOnSave(@event.Entity);
		}

		#endregion

		#region IPostInsertEventListener Members

		public void OnPostInsert(PostInsertEvent @event)
		{
			RaiseOnSave(@event.Entity);
		}

		#endregion
	}
}
