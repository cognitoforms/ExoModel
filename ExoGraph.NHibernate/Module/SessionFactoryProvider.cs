using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;

namespace ExoGraph.NHibernate.Module
{
	/// <summary>
	/// Acts as a level of indirection between the actual SessionFactory and the mode of storage
	/// </summary>
	/// <remarks>
	/// This class must be initialized by any application making use of NHibernateSessionModule
	/// </remarks>
	public class SessionFactoryProvider
	{
		public static readonly SessionFactoryProvider Current = new SessionFactoryProvider();

		private ISessionFactory sessionFactory;

		public ISessionFactory SessionFactory
		{
			get
			{
				if (sessionFactory == null)
					throw new Exception("SessionFactoryProvider not initialized!");

				return sessionFactory;
			}
			set
			{
				sessionFactory = value;
			}
		}

		protected SessionFactoryProvider()
		{
		}
	}
}
