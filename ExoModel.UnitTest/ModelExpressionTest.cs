using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Linq.Expressions;
using ExoModel.UnitTest.Models.Movies;

namespace ExoModel.UnitTest
{
	/// <summary>
	/// Tests the expression capabilities of ExoModel.
	/// </summary>
	[TestClass]
	public class ModelExpressionTest
	{
		[ClassInitialize]
		public static void Initialize(TestContext options)
		{
			Models.Movies.Model.InitializeTestModel();
		}

		/// <summary>
		/// Verifies that the test is at least able to load movie data based on a 
		/// successful call to <see cref="Model.InitializeTestModel"/>.
		/// </summary>
		[TestMethod]
		public void LoadTest()
		{
			var movies = Movie.All;

			Assert.IsTrue(movies.Count > 0, "Unable to load test movie data.");
		}

		/// <summary>
		/// Validates model path building based on <see cref="Expression"/> instances
		/// using the request model types.
		/// </summary>
		[TestMethod]
		public void TestPathBuilding()
		{
			// Name of user assigned to request
			TestPath((Request r) =>
				r.AssignedTo.UserName,
				"AssignedTo.UserName");

			// Requests assigned to myself
			TestPath((User u) =>
				u.Requests.Any(r => r.AssignedTo == u),
				"Requests.AssignedTo");

			// Projection of requests into an anonymous type
			TestPath((User u) =>
				u.Requests.Select(r => new { Description = r.Description, User = r.User.UserName }),
				"Requests{Description,User.UserName}");

			// Walk the same path multiple times
			TestPath((User u) => new
				{
					HighPriority = u.Assignments.Count(r => r.Priority.Name == "High"),
					MediumPriority = u.Assignments.Count(r => r.Priority.Name == "Medium"),
					LowPriority = u.Assignments.Count(r => r.Priority.Name == "Low")
				},
				"Assignments.Priority.Name");

			// Walk different paths from the same root, including a parent closure scope variable
			TestPath((User u) =>
				u.Assignments.Count(r => u.Requests.Contains(r) && r.Priority.Name == "High"),
				"{Assignments.Priority.Name,Requests}");

			// Used chained linq extension methods
			TestPath((Request r) =>
				r.User.Assignments.Where(a => a.Priority.Name == "High").Select(a => a.Description),
				"User.Assignments{Priority.Name,Description}");
		}

		/// <summary>
		/// Calculates the path for a given expression and compares it to the expected path.
		/// </summary>
		/// <typeparam name="TRoot"></typeparam>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="expression"></param>
		/// <param name="path"></param>
		void TestPath<TRoot, TResult>(Expression<Func<TRoot, TResult>> expression, string expectedPath)
		{
			Assert.AreEqual(expectedPath, ModelContext.Current.GetModelType<TRoot>().GetPath(expression).Path);
		}

		[TestMethod]
		public void TestExpressionParsing()
		{
			// Get the Robin Hood movie
			var movie = Movie.All.Where(m => m.Name == "Robin Hood").First();

			// Director.Person = "Ridley Scott"
			TestExpression(movie, "Director.Person", "Ridley Scott", "Director.Person");

			// Genres.Count() == Roles.Count() = true
			TestExpression(movie, "Genres.Count() = Roles.Count()", true, "{Genres,Roles}");

			// Genres.Count() + Roles.Count() = 6
			TestExpression(movie, "Genres.Count() + Roles.Count()", 6, "{Genres,Roles}");

			// Roles.Where(Star).Count() + Roles.Where(Lead).Count() = 4
			TestExpression(movie, "Roles.Where(Star).Count + Roles.Where(Lead).Count", 4, "Roles{Star,Lead}");

			// Roles.Where(Star).Sum(Order) = 3
			TestExpression(movie, "Roles.Where(Star).Sum(Order)", 3, "Roles{Star,Order}");
		}

		/// <summary>
		/// Parses the specified expression and verifies that both the computed path and expected results are achieved
		/// </summary>
		/// <typeparam name="TRoot"></typeparam>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="instance"></param>
		/// <param name="expression"></param>
		/// <param name="expectedValue"></param>
		/// <param name="expectedPath"></param>
		void TestExpression<TRoot, TResult>(TRoot instance, string expression, TResult expectedValue, string expectedPath)
		{
			var exp = typeof(TRoot).GetModelType().GetExpression<TResult>(expression);

			// Ensure the computed path is correct
			Assert.AreEqual(expectedPath, exp.Path.Path);

			// Ensure the expression yields the correct value
			Assert.AreEqual(expectedValue, exp.Expression.Compile().DynamicInvoke(instance));
		}
	}
}
