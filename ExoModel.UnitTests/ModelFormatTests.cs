using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExoModel.UnitTests.Models;
using ExoModel.UnitTests.Models.Requests;

namespace ExoModel.UnitTests
{
	/// <summary>
	/// Tests the formatting capabilities of ExoModel.
	/// </summary>
	[TestClass]
	[TestModel(Name = "Requests")]
	public class ModelFormatTests : TestModelBase
	{
		[TestMethod]
		public void TestModelInstanceFormatting()
		{
			var request = new Request
			{
				AssignedTo = new User	{ UserName = "Billy Bob" },
				Category = Context.FetchAll<Category>().Single(c => c.Name == "Server"),
				Description = "This is a really good test description!!!",
				Priority = Context.FetchAll<Priority>().Single(p => p.Name == "High (2 Hour)"),
				User = new User { UserName = "Billy Bob's Worst Nightmare", IsActive = false }
			};

			Assert.AreEqual("Billy Bob", ModelContext.Current.GetModelInstance(request).ToString("[AssignedTo.UserName]"));
			Assert.AreEqual("Billy Bob is assigned to a High (2 Hour) priority Server request for Billy Bob's Worst Nightmare.",
				ModelContext.Current.GetModelInstance(request).ToString("[AssignedTo.UserName] is assigned to a [Priority.Name] priority [Category.Name] request for [User.UserName]."));
			Assert.AreEqual("Inactive", ModelContext.Current.GetModelInstance(request).ToString(@"[User.IsActive:Ac\]tive;Inactive]"));
		}
	}
}
