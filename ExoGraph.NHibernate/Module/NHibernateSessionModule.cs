using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using NHibernate.Context;

namespace ExoGraph.NHibernate.Module
{
	/// <summary>
	/// Creates an NHibernate session for use by ExoGraph on a by-request basis
	/// </summary>
	public class NHibernateSessionModule : IHttpModule
	{
		#region IHttpModule Members

		public void Dispose()
		{
		}

		public void Init(HttpApplication context)
		{
			context.BeginRequest += delegate 
			{
				var session = SessionFactoryProvider.Current.SessionFactory.OpenSession();

				CurrentSessionContext.Bind(session);
			};

			context.EndRequest += delegate
			{
				CurrentSessionContext.Unbind(SessionFactoryProvider.Current.SessionFactory);
			};
		}

		#endregion
	}
}
