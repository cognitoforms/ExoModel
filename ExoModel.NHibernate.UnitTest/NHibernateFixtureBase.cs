using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoGraph.UnitTest;
using NHibernate.Engine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using log4net;
using ExoGraph.NHibernate.Collection;

using Environment = NHibernate.Cfg.Environment;
using NHibernate.Cfg;
using NHibernate;
using NHibernate.Context;

namespace ExoGraph.NHibernate.UnitTest
{
	public class NHibernateFixtureBase : GraphContextTest<User, Category, Priority, Request, IList<Request>, IList<Category>>
	{
		protected Configuration config;
		protected ISessionFactoryImplementor sessions;
		protected NHibernateGraphTypeProvider provider;

		[TestInitialize]
		public void TestFixtureSetup()
		{
			log4net.Config.XmlConfigurator.Configure();

			var interceptor = new DataBindingInterceptor();

			config = new Configuration();
			config.SetInterceptor(interceptor);
			config.Properties[Environment.CollectionTypeFactoryClass] = typeof(ObservableCollectionTypeFactory).AssemblyQualifiedName;
			config.Properties[Environment.CurrentSessionContextClass] = "thread_static";
			config.SetListener(global::NHibernate.Event.ListenerType.PostInsert, SaveNotificationEventListener.Instance);
			config.SetListener(global::NHibernate.Event.ListenerType.PostUpdate, SaveNotificationEventListener.Instance);
			config.Configure();
			sessions = (ISessionFactoryImplementor) config.BuildSessionFactory();

			interceptor.SessionFactory = sessions;

			provider = new NHibernateGraphTypeProvider(string.Empty, new Type[] { typeof(Request), typeof(Priority), typeof(User), typeof(Category) });

			provider.SessionFactory = sessions;
			CurrentSessionContext.Bind(sessions.OpenSession());
		}

		[TestCleanup]
		public void TestFixtureTeardown()
		{
			CurrentSessionContext.Unbind(sessions);
		}
	}
}
