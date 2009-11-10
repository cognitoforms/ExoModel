using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExoGraph.UnitTest;

namespace ExoGraph.Injection.UnitTest
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

	public class ConcreteInjectionGraphContext : InjectionGraphContext
	{
		public ConcreteInjectionGraphContext()
			: base(new Type[] { typeof(CustomerBase), typeof(Customer), typeof(Contact) })
		{ }

		public override object CreateInstance(GraphType type, string id)
		{
			return Type.GetType(type.Name).GetConstructor(Type.EmptyTypes).Invoke(null);
		}

		protected override void DeleteInstance(object instance)
		{ }
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
