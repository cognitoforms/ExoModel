using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Proxy;
using Castle.DynamicProxy;
using NHibernate;
using NHibernate.ByteCode.Castle;

namespace ExoGraph.NHibernate
{
	public class ProxyFactory : AbstractProxyFactory
	{
		private static readonly ProxyGenerator ProxyGenerator = new ProxyGenerator();

		protected static ProxyGenerator DefaultProxyGenerator
		{
			get { return ProxyGenerator; }
		}

		public override INHibernateProxy GetProxy(object id, global::NHibernate.Engine.ISessionImplementor session)
		{
			try
			{
				var initializer = new LazyInitializer(EntityName, PersistentClass, id, GetIdentifierMethod, SetIdentifierMethod, ComponentIdType, session);

				object generatedProxy = IsClassProxy
											? DataBindingFactory.Create(PersistentClass, Interfaces, initializer)
											: ProxyGenerator.CreateInterfaceProxyWithoutTarget(Interfaces[0], Interfaces,
																								initializer);

				initializer._constructed = true;
				return (INHibernateProxy) generatedProxy;
			}
			catch (Exception e)
			{
				throw new HibernateException("Creating a proxy instance failed", e);
			}
		}
	}
}
