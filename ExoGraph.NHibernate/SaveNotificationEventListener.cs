using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Event;
using ExoGraph.Injection;

namespace ExoGraph.NHibernate
{
	public class SaveNotificationEventListener : IPostUpdateEventListener, IPostInsertEventListener
	{
		public static readonly SaveNotificationEventListener Instance = new SaveNotificationEventListener();

		private SaveNotificationEventListener()
		{
		}

		private void RaiseOnSave(object entity)
		{
			if (entity is ExoGraph.NHibernate.DataBindingFactory.IGraphInstance)
				((NHibernateGraphContext) GraphContext.Current).OnSave(((ExoGraph.NHibernate.DataBindingFactory.IGraphInstance) entity).Instance);
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
