using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExoGraph.UnitTest;

namespace ExoGraph.Injection.UnitTests
{
	/// <summary>
	/// Performs unit testing for the <see cref="InjectionGraphContext"/> context implementation.
	/// </summary>
	[TestClass]
	public class InjectionContextTest : ContextTestBase
	{
		public InjectionContextTest()
		{ }
	}

	#region ConcreteInjectionGraphContext

	public class ConcreteInjectionGraphTypeProvider : InjectionGraphTypeProvider
	{
		public ConcreteInjectionGraphTypeProvider()
			: base(string.Empty, new Type[] { typeof(CustomerBase), typeof(Customer), typeof(Contact) })
		{ }

		protected override StrongGraphType CreateGraphType(string @namespace, Type type, Func<GraphInstance, object> extensionFactory)
		{
			return new ConcreteInjectionGraphType(@namespace, type, extensionFactory);
		}

		protected class ConcreteInjectionGraphType : InjectionGraphType
		{
			public ConcreteInjectionGraphType(string @namespace, Type type, Func<GraphInstance, object> extensionFactory)
				: base(@namespace, type, extensionFactory)
			{ }

			protected override void SaveInstance(GraphInstance graphInstance)
			{
				throw new NotImplementedException();
			}

			protected override string GetId(object instance)
			{
				throw new NotImplementedException();
			}

			protected override object GetInstance(string id)
			{
				return Type.GetType(this.Name).GetConstructor(Type.EmptyTypes).Invoke(null);
			}

			protected override void DeleteInstance(GraphInstance graphInstance)
			{
				throw new NotImplementedException();
			}
		}
	}

	#endregion

	#region CustomerBase

	[ExoGraph]
	public abstract class CustomerBase
	{
		public string Name { get; set; }
	}

	#endregion

	#region Customer

	[ExoGraph]
	public class Customer : CustomerBase
	{
		List<Contact> otherContacts = new List<Contact>();

		public Contact PrimaryContact { get; set; }

		public int YearFounded { get; set; }

		public List<Contact> OtherContacts { get { return otherContacts; } }
	}

	#endregion

	#region Contact

	[ExoGraph]
	public class Contact
	{
		public string Name { get; set; }

		public string Phone { get; set; }

		public string Email { get; set; }
	}

	#endregion
}
