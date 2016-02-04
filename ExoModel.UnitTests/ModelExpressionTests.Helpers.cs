using System;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExoModel.UnitTests
{
	public partial class ModelExpressionTests
	{
		/// <summary>
		/// Calculates the path for a given expression and compares it to the expected path.
		/// </summary>
		private static void TestPath<TRoot, TResult>(Expression<Func<TRoot, TResult>> expression, string expectedPath)
		{
			Assert.AreEqual(expectedPath, ModelContext.Current.GetModelType<TRoot>().GetPath(expression).Path);
		}

		/// <summary>
		/// Parses the specified expression and verifies that both the computed path and expected results are achieved
		/// </summary>
		private static void TestExpression<TRoot, TResult>(TRoot instance, string expression, TResult expectedValue, string expectedPath)
			where TRoot : class
		{
			var exp = typeof(TRoot).GetModelType().GetExpression<TResult>(expression);

			// Ensure the computed path is correct
			Assert.AreEqual(expectedPath, exp.Path.Path);

			// Ensure the expression yields the correct value
			Assert.AreEqual(expectedValue, exp.Invoke(ModelContext.Current.GetModelInstance(instance)),
				"Expected result of expression '" + expression +
				"' to be " +
				(typeof(TResult) == typeof(string) ? "\"" + expectedValue + "\"" : (expectedValue != null ? expectedValue.ToString() : "null"))
				+ ".");
		}
	}
}
