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
	public class RowModelTypeProviderSuite
	{
		private RowModelTypeProvider provider;
		private ITranslator translator;

		[TestInitialize()]
		public void Before()
		{
			translator = new MapBasedTranslator();
			provider = new RowModelTypeProvider(translator);

			new ModelContextProvider().CreateContext += (source, args) =>
			{
				args.Context = new ModelContext(provider);
			};
		}

		[TestMethod]
		public void TestLoadTypeCreatesType()
		{
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Location");
			provider.CreateType(propertyNames, out outType, "TypeName");

			//Verify the new type was created
			Assert.AreEqual(true, provider.Types.ContainsKey("TypeName"));
			Assert.AreEqual("TypeName", outType.Name);

			//Verify the properties on created type
			Assert.AreEqual(1, outType.Properties.Count);
			Assert.AreEqual("_Location", outType.Properties.First().Name);
			Assert.AreNotEqual(null, outType.Properties["_Location"]);
		}

		[TestMethod]
		public void TestContextCreationGetType()
		{
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Location");
			provider.CreateType(propertyNames, out outType, "TypeName");

			//now that the type has been created, try to retrieve the type from the context
			ModelType retVal = ModelContext.Current.GetModelType("TypeName");
			Assert.AreEqual("TypeName", retVal.Name);

		}

		[TestMethod]
		public void TestContextCreationGetInstance()
		{
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Employee Id");
			propertyNames.Add("Location");
			propertyNames.Add("FirstName");
			propertyNames.Add("LastName");
			provider.CreateType(propertyNames, out outType, "TypeName");

			//load a new instance
			string employeeId = "1";
			IList<string> instanceValues = new List<string>();
			instanceValues.Add(employeeId);
			instanceValues.Add("23");
			instanceValues.Add("John");
			instanceValues.Add("Doe");
			provider.CreateInstance(outType, instanceValues);
			RowInstance instance = provider.GetInstance(outType, employeeId);

			//now that the type has been created, try to retrieve the type from the context
			ModelInstance contextInstance = ModelContext.Current.GetModelInstance(instance);
			Assert.AreEqual(instance["_Employee_Id"], contextInstance["_Employee_Id"]);
		}

		[TestMethod]
		public void TestLoadTypeDuplicateTypeNames()
		{
			ModelType outType;
			ModelType outTypeSecond;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Location");
			provider.CreateType(propertyNames, out outType, "TypeName");
			provider.CreateType(propertyNames, out outTypeSecond, "TypeName");

			//verify that the second type name is not "TypeName" because you can't have duplicates
			Assert.AreEqual("TypeName", outType.Name);
			Assert.AreNotEqual("TypeName", outTypeSecond.Name);
		}

		[TestMethod]
		public void TestLoadInstance()
		{
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Employee Id");
			propertyNames.Add("Location");
			propertyNames.Add("FirstName");
			propertyNames.Add("LastName");
			provider.CreateType(propertyNames, out outType, "TypeName");

			//load a new instance
			string employeeId = "1";
			IList<string> instanceValues = new List<string>();
			instanceValues.Add(employeeId);
			instanceValues.Add("23");
			instanceValues.Add("John");
			instanceValues.Add("Doe");
			provider.CreateInstance(outType, instanceValues);

			//retrieve the instance
			var instance = provider.GetInstance(outType, employeeId);

			Assert.AreEqual(employeeId, instance["_Employee_Id"]);
			Assert.AreEqual("23", instance["_Location"]);
			Assert.AreEqual("John", instance["_FirstName"]);
			Assert.AreEqual("Doe", instance["_LastName"]);
		}

		[TestCleanup()]
		public void After()
		{
		}
	}
}
