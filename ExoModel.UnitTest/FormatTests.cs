using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace ExoModel.UnitTest
{
	/// <summary>
	/// Tests the formatting capabilities of ExoModel.
	/// </summary>
	[TestClass]
	public class FormatTests
	{
		[ClassInitialize]
		public static void Initialize(TestContext options)
		{
			// Initialize the model context to use the test model type provider
			ModelContext.Init(
				() => {},
				new TestModelTypeProvider(Assembly.GetExecutingAssembly()));
		}

		[TestMethod]
		public void TestModelInstanceFormatting()
		{
			var request = new Request()
			{
				AssignedTo = new User()	{ UserName = "Billy Bob" },
				Category = new Category() { Name = "Server Support" },
				Description = "This is a really good test description!!!",
				Priority = new Priority() { Name = "High" },
				User = new User() { UserName = "Billy Bob's Worst Nightmare", IsActive = false }
			};

			Assert.AreEqual("Billy Bob", ModelContext.Current.GetModelInstance(request).ToString("[AssignedTo.UserName]"));
			Assert.AreEqual("Billy Bob is assigned to a High priority Server Support request for Billy Bob's Worst Nightmare.",
				ModelContext.Current.GetModelInstance(request).ToString("[AssignedTo.UserName] is assigned to a [Priority.Name] priority [Category.Name] request for [User.UserName]."));
			Assert.AreEqual("Inactive", ModelContext.Current.GetModelInstance(request).ToString(@"[User.IsActive:Ac\]tive;Inactive]"));
		}
	}
}
