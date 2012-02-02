using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace ExoGraph.UnitTest
{
	/// <summary>
	/// Tests the formatting capabilities of ExoGraph.
	/// </summary>
	[TestClass]
	public class FormatTests
	{
		[ClassInitialize]
		public static void Initialize(TestContext options)
		{
			// Initialize the graph context to use the test graph type provider
			GraphContext.Init(
				() => {},
				new TestGraphTypeProvider(Assembly.GetExecutingAssembly()));
		}

		[TestMethod]
		public void TestGraphInstanceFormatting()
		{
			var request = new Request()
			{
				AssignedTo = new User()	{ UserName = "Billy Bob" },
				Category = new Category() { Name = "Server Support" },
				Description = "This is a really good test description!!!",
				Priority = new Priority() { Name = "High" },
				User = new User() { UserName = "Billy Bob's Worst Nightmare", IsActive = false }
			};

			Assert.AreEqual("Billy Bob", GraphContext.Current.GetGraphInstance(request).ToString("[AssignedTo.UserName]"));
			Assert.AreEqual("Billy Bob is assigned to a High priority Server Support request for Billy Bob's Worst Nightmare.",
				GraphContext.Current.GetGraphInstance(request).ToString("[AssignedTo.UserName] is assigned to a [Priority.Name] priority [Category.Name] request for [User.UserName]."));
			Assert.AreEqual("Inactive", GraphContext.Current.GetGraphInstance(request).ToString(@"[User.IsActive:Ac\]tive;Inactive]"));
		}
	}
}
