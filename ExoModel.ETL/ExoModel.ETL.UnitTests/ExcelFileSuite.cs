using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExoModel.ETL;
using ExoModel;
using ExoModel.Json;
using System.IO;

namespace ExoModel.ETL.UnitTests
{
	[TestClass]
	public class ExcelFileSuite
	{
		private RowModelTypeProvider dynamicProvider;
		private ITranslator translator;
		private readonly string testFilePath = "..\\..\\..\\ExoModel.ETL.UnitTests\\TestData\\TestExcelData.xlsx";

		[TestInitialize()]
		public void Before()
		{
			translator = new MapBasedTranslator();
			dynamicProvider = new RowModelTypeProvider(translator);

			new ModelContextProvider().CreateContext += (source, args) =>
			{
				args.Context = new ModelContext(dynamicProvider);
			};
		}

		[TestMethod]
		public void TestTypeCreation()
		{
			//Read a test excel file as a byte array
			FileStream fileStream = new FileStream(testFilePath, FileMode.Open, FileAccess.Read);
			ExcelFile file = new ExcelFile(fileStream, dynamicProvider);

			//verify the types were created successfully.
			Assert.AreEqual("Data", ModelContext.Current.GetModelType("Data").Name);
			Assert.AreEqual("SecondData", ModelContext.Current.GetModelType("SecondData").Name);
		}

		[TestMethod]
		public void TestTypeStorage()
		{
			//Read a test excel file as a byte array
			FileStream fileStream = new FileStream(testFilePath, FileMode.Open, FileAccess.Read);
			ExcelFile file = new ExcelFile(fileStream, dynamicProvider);

			//verify the types were created successfully.
			//and stored in the file object correctly
			Assert.AreEqual("Data", file.GetTypesGenerated().ElementAt(0).Name);
			Assert.AreEqual("SecondData", file.GetTypesGenerated().ElementAt(1).Name);
		}

		[TestMethod]
		public void TestFirstColumnIsId()
		{
			//Read a test excel file as a byte array
			FileStream fileStream = new FileStream(testFilePath, FileMode.Open, FileAccess.Read);
			ExcelFile file = new ExcelFile(fileStream, dynamicProvider);

			//verify the types were created successfully.
			Assert.AreEqual("100024", file.GetInstances(ModelContext.Current.GetModelType("Data")).ElementAt(0).Id );
			Assert.AreEqual("101707", file.GetInstances(ModelContext.Current.GetModelType("Data")).ElementAt(1).Id);
			Assert.AreEqual("101708", file.GetInstances(ModelContext.Current.GetModelType("Data")).ElementAt(2).Id);
			Assert.AreEqual("101713", file.GetInstances(ModelContext.Current.GetModelType("Data")).ElementAt(3).Id);
		}

		[TestMethod]
		public void TestInstanceDataCreation()
		{
			//Read a test excel file as a byte array
			FileStream fileStream = new FileStream(testFilePath, FileMode.Open, FileAccess.Read);
			ExcelFile file = new ExcelFile(fileStream, dynamicProvider);

			//verify the first instance was created
			//need to use the translated property name
			Assert.AreEqual("KELLER", file.GetInstances(ModelContext.Current.GetModelType("Data")).ElementAt(0)["_LastName"]);
			Assert.AreEqual("RHONDA", file.GetInstances(ModelContext.Current.GetModelType("Data")).ElementAt(0)["_FirstName"]);
			Assert.AreEqual("513826", file.GetInstances(ModelContext.Current.GetModelType("Data")).ElementAt(0)["_Customer__"]);
			Assert.AreEqual("100024", file.GetInstances(ModelContext.Current.GetModelType("Data")).ElementAt(0)["_Location__"]);
		}

		[TestCleanup()]
		public void After()
		{

		}
	}
}
