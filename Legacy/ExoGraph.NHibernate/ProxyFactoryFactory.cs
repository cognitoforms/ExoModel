using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Bytecode;
using NHibernate.Proxy;

namespace ExoGraph.NHibernate
{
	public class ProxyFactoryFactory : IProxyFactoryFactory
	{

		#region IProxyFactoryFactory Members

		public global::NHibernate.Proxy.IProxyFactory BuildProxyFactory()
		{
			return new ProxyFactory();
		}

		public global::NHibernate.Proxy.IProxyValidator ProxyValidator
		{
			get { return new DynProxyTypeValidator(); }
		}

		#endregion
	}
}
