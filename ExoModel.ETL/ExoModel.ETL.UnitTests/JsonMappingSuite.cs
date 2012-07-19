using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExoModel.ETL;
using ExoModel;

namespace ExoModel.ETL.UnitTests
{
	[TestClass]
	public class JsonMappingSuite
	{
		[TestInitialize()]
		public void Before()
		{
		}

		[TestMethod]
		public void TestCreation()
		{
			string mapping = @"[{PropertyPath : 'Number',Expression : '[Location #]'}, {PropertyPath : 'ServiceAddress.Line1',Expression : '[Full Service Address]'}, {PropertyPath : 'ServiceAddress.City',Expression : '[Service City]'}]";

			JsonMapping map = new JsonMapping(mapping);
			ExpressionToProperty test = new ExpressionToProperty();
			test.Expression = "[Service City]";
			test.PropertyPath = "ServiceAddress.City";

			Assert.AreEqual(3, map.GetMapping().Count());
			Assert.AreEqual(true, test.Equals(map.GetMapping().ElementAt(2)));
		}

		[TestCleanup()]
		public void After()
		{
		}
	}
}
