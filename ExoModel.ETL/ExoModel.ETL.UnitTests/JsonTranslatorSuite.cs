using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExoModel.ETL;
using ExoModel;
using ExoModel.Json;

namespace ExoModel.ETL.UnitTests
{
	[TestClass]
	public class JsonTranslatorSuite
	{
		private RowModelTypeProvider dynamicProvider;
		private JsonModel staticProvider;
		private ITranslator translator;

		[TestInitialize()]
		public void Before()
		{
			translator = new MapBasedTranslator();
			dynamicProvider = new RowModelTypeProvider(translator);
			staticProvider = new JsonModel();

			new ModelContextProvider().CreateContext += (source, args) =>
			{
				args.Context = new ModelContext(dynamicProvider, staticProvider);
			};
			
			//build up the mock types by utilizing JsonModel Provider
			//will be the root object.
			string mockAccount = @"{
									'types' : {
										'Payment' : {
											'properties' : {
												'amount' : {
													type : 'Number'
												}
											}
										},
										'Account' : {
											'properties' : {
												'firstName' : {
													type : 'String'
												},
												'lastName' : {
													type : 'String'
												},
												'accountNumber' : {
													type : 'Number'
												},
												'payments' : {
													type : 'Payment',
													isList : true
												}
											}
										}
									},
									'instances' : {
										'Account' : {
											'1' : ['Test', 'User', '1234', ['1']]
										},
										'Payment' : {
											'1' : [23.05]
										}
									}
								}
								";

			staticProvider.Load(mockAccount);
		}

		[TestMethod]
		public void TestLoadTypeTranslatesPropertyNames()
		{
			//Verify the properties were added and translated appropriately
			Assert.AreEqual("_Location", translator.AddTranslation("Location"));
			Assert.AreEqual("_Customer", translator.AddTranslation("Customer"));
			Assert.AreEqual("_FirstName__", translator.AddTranslation("FirstName#?"));
			Assert.AreEqual("_1234", translator.AddTranslation("1234"));
		}

		[TestMethod]
		public void TestLoadTypeTranslatesPropertyNamesWithDuplicates()
		{
			Assert.AreEqual("_Location", translator.AddTranslation("Location"));
			Assert.AreEqual("_Location1", translator.AddTranslation("Location"));
			Assert.AreEqual("_Location2", translator.AddTranslation("Location"));
			Assert.AreEqual("_Location3", translator.AddTranslation("Location"));
		}

		[TestMethod]
		public void TestDualContext()
		{
			///This method will make sure that both the static and dynamic context's are available
			///for use by the translator.
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Employee Id");
			propertyNames.Add("Location");
			propertyNames.Add("FirstName");
			propertyNames.Add("LastName");
			dynamicProvider.CreateType(propertyNames, out outType, "TypeName");

			//now that the type has been created, try to retrieve the type from the context
			ModelType dynamicType = ModelContext.Current.GetModelType("TypeName");
			Assert.AreEqual("TypeName", dynamicType.Name);

			ModelType staticType = ModelContext.Current.GetModelType("Account");
			Assert.AreEqual("Account", staticType.Name);
		}

		[TestMethod]
		public void TestExpressionTranslation()
		{
			string translatedName = translator.AddTranslation("[Payment Date]");
			Assert.AreEqual("__Payment_Date_.Length > 0 and DateTime(__Payment_Date_) > DateTime.Today.AddYear(-1) ? __Payment_Date_ : null", translator.TranslateExpression("[Payment Date].Length > 0 and DateTime([Payment Date]) > DateTime.Today.AddYear(-1) ? [Payment Date] : null"));
		}

		[TestMethod]
		public void TestSimpleInstanceTranslation()
		{
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Employee Id");
			propertyNames.Add("Location");
			propertyNames.Add("FirstName");
			propertyNames.Add("LastName");
			dynamicProvider.CreateType(propertyNames, out outType, "TypeName");

			//load a new instance
			IList<string> instanceValues = new List<string>();
			instanceValues.Add("1");
			instanceValues.Add("23");
			instanceValues.Add("John");
			instanceValues.Add("Doe");
			dynamicProvider.CreateInstance(outType, instanceValues);

			instanceValues = new List<string>();
			instanceValues.Add("2");
			instanceValues.Add("23");
			instanceValues.Add("Jane");
			instanceValues.Add("Doe");
			dynamicProvider.CreateInstance(outType, instanceValues);

			string mapping = @"[{PropertyPath : 'firstName',Expression : 'FirstName'}, {PropertyPath : 'lastName',Expression : 'LastName'}, {PropertyPath : 'accountNumber',Expression : 'Employee Id'}]";

			JsonMapping map = new JsonMapping(mapping);
			IEnumerable<ModelInstance> translatedInstances = translator.Translate(ModelContext.Current.GetModelType("Account"), outType, dynamicProvider.GetModelInstances(ModelContext.Current.GetModelType("TypeName")), map);

			Assert.AreEqual(translatedInstances.ElementAt(0)["firstName"], "John");
			Assert.AreEqual(translatedInstances.ElementAt(0)["lastName"], "Doe");
			Assert.AreEqual(translatedInstances.ElementAt(0)["accountNumber"], "1");

			Assert.AreEqual(translatedInstances.ElementAt(1)["firstName"], "Jane");
			Assert.AreEqual(translatedInstances.ElementAt(1)["lastName"], "Doe");
			Assert.AreEqual(translatedInstances.ElementAt(1)["accountNumber"], "2");
		}

		[TestMethod]
		public void TestComplexExressions()
		{
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Employee Id");
			propertyNames.Add("Location");
			propertyNames.Add("FirstName");
			propertyNames.Add("LastName");
			dynamicProvider.CreateType(propertyNames, out outType, "TypeName");

			//load a new instance
			IList<string> instanceValues = new List<string>();
			instanceValues.Add("1");
			instanceValues.Add("23");
			instanceValues.Add("John");
			instanceValues.Add("Doe");
			dynamicProvider.CreateInstance(outType, instanceValues);

			instanceValues = new List<string>();
			instanceValues.Add("2");
			instanceValues.Add("23");
			instanceValues.Add("Jane");
			instanceValues.Add("Doe");
			dynamicProvider.CreateInstance(outType, instanceValues);

			string mapping = "[{PropertyPath : 'firstName',Expression : 'FirstName == \"John\" ? \"MyNameIsJohn\" : \"MyNameIsJohn\"'}, {PropertyPath : 'lastName',Expression : 'LastName.Length'}, {PropertyPath : 'accountNumber',Expression : 'Employee Id + LastName'}]";

			JsonMapping map = new JsonMapping(mapping);
			IEnumerable<ModelInstance> translatedInstances = translator.Translate(ModelContext.Current.GetModelType("Account"), outType, dynamicProvider.GetModelInstances(ModelContext.Current.GetModelType("TypeName")), map);

			Assert.AreEqual(translatedInstances.ElementAt(0)["firstName"], "MyNameIsJohn");
			Assert.AreEqual(translatedInstances.ElementAt(0)["lastName"], 3);
			Assert.AreEqual(translatedInstances.ElementAt(0)["accountNumber"], "1Doe");

			Assert.AreEqual(translatedInstances.ElementAt(1)["firstName"], "MyNameIsJohn");
			Assert.AreEqual(translatedInstances.ElementAt(1)["lastName"], 3);
			Assert.AreEqual(translatedInstances.ElementAt(1)["accountNumber"], "2Doe");
		}

		[TestMethod]
		public void TestStringManipulationExpressions()
		{
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Employee Id");
			propertyNames.Add("Location");
			propertyNames.Add("FirstName");
			propertyNames.Add("LastName");
			dynamicProvider.CreateType(propertyNames, out outType, "TypeName");

			//load a new instance
			IList<string> instanceValues = new List<string>();
			instanceValues.Add("1");
			instanceValues.Add("23");
			instanceValues.Add("John");
			instanceValues.Add("Doe");
			dynamicProvider.CreateInstance(outType, instanceValues);

			instanceValues = new List<string>();
			instanceValues.Add("2");
			instanceValues.Add("23");
			instanceValues.Add("Jane");
			instanceValues.Add("Doe");
			dynamicProvider.CreateInstance(outType, instanceValues);

			string mapping = "[{PropertyPath : 'firstName',Expression : 'FirstName + LastName'},{PropertyPath : 'lastName',Expression : 'FirstName.Contains(\"John\") ? \"MyNameIsJohn\" : \"MyNameIsNotJohn\"'},{PropertyPath : 'accountNumber',Expression : 'FirstName.Substring(0,1).ToLower()'}]";

			JsonMapping map = new JsonMapping(mapping);
			IEnumerable<ModelInstance> translatedInstances = translator.Translate(ModelContext.Current.GetModelType("Account"), outType, dynamicProvider.GetModelInstances(ModelContext.Current.GetModelType("TypeName")), map);

			Assert.AreEqual(translatedInstances.ElementAt(0)["firstName"], "JohnDoe");
			Assert.AreEqual(translatedInstances.ElementAt(0)["lastName"], "MyNameIsJohn");
			Assert.AreEqual(translatedInstances.ElementAt(0)["accountNumber"], "j");
			Assert.AreEqual(translatedInstances.ElementAt(1)["firstName"], "JaneDoe");
			Assert.AreEqual(translatedInstances.ElementAt(1)["lastName"], "MyNameIsNotJohn");
			Assert.AreEqual(translatedInstances.ElementAt(1)["accountNumber"], "j");
		}

		[TestMethod]
		public void TestArrayIndexPropertyPath()
		{
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Employee Id");
			propertyNames.Add("Location");
			propertyNames.Add("FirstName");
			propertyNames.Add("LastName");
			dynamicProvider.CreateType(propertyNames, out outType, "TypeName");

			//load a new instance
			IList<string> instanceValues = new List<string>();
			instanceValues.Add("1");
			instanceValues.Add("23");
			instanceValues.Add("John");
			instanceValues.Add("Doe");
			dynamicProvider.CreateInstance(outType, instanceValues);

			instanceValues = new List<string>();
			instanceValues.Add("2");
			instanceValues.Add("23");
			instanceValues.Add("Jane");
			instanceValues.Add("Doe");
			dynamicProvider.CreateInstance(outType, instanceValues);

			ModelType staticType = ModelContext.Current.GetModelType("Payment");
			Assert.AreEqual("Payment", staticType.Name);

			string mapping = @"[{PropertyPath : 'payments[0].amount',Expression : 'Employee Id'}]";

			JsonMapping map = new JsonMapping(mapping);
			IEnumerable<ModelInstance> translatedInstances = translator.Translate(ModelContext.Current.GetModelType("Account"), outType, dynamicProvider.GetModelInstances(ModelContext.Current.GetModelType("TypeName")), map);

			Assert.AreEqual(((ModelInstance)translatedInstances.ElementAt(0).GetList("payments").ElementAt(0))["amount"], "1");
			Assert.AreEqual(((ModelInstance)translatedInstances.ElementAt(1).GetList("payments").ElementAt(0))["amount"], "2");
		}

		[TestMethod]
		public void TestTranslationOfExistingDestinationObject()
		{
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Employee Id");
			propertyNames.Add("Location");
			propertyNames.Add("FirstName");
			propertyNames.Add("LastName");
			dynamicProvider.CreateType(propertyNames, out outType, "TypeName");

			//load a new instance
			IList<string> instanceValues = new List<string>();
			instanceValues.Add("1");
			instanceValues.Add("23");
			instanceValues.Add("John");
			instanceValues.Add("Doe");
			dynamicProvider.CreateInstance(outType, instanceValues);

			string mapping = @"[{PropertyPath : 'firstName',Expression : 'FirstName'}]";

			JsonMapping map = new JsonMapping(mapping);

			//The test will overwrite the existing account object with new values from the source entity.
			IEnumerable<ModelInstance> translatedInstances = translator.Translate(ModelContext.Current.GetModelType("Account"), outType, dynamicProvider.GetModelInstances(ModelContext.Current.GetModelType("TypeName")), map, (type, instance, id) =>
													{
														return type.Create("1");
													});

			Assert.AreEqual(translatedInstances.ElementAt(0)["firstName"], "John");
			Assert.AreEqual(translatedInstances.ElementAt(0)["lastName"], "User");
			Assert.AreEqual(translatedInstances.ElementAt(0)["accountNumber"], "1234");
		}

		[TestMethod]
		public void TestTranslationOfExistingDestinationObjectWithIdInMapping()
		{
			ModelType outType;
			IList<string> propertyNames = new List<string>();
			propertyNames.Add("Employee Id");
			propertyNames.Add("Location");
			propertyNames.Add("FirstName");
			propertyNames.Add("LastName");
			dynamicProvider.CreateType(propertyNames, out outType, "TypeName");

			//load a new instance
			IList<string> instanceValues = new List<string>();
			instanceValues.Add("1");
			instanceValues.Add("23");
			instanceValues.Add("John");
			instanceValues.Add("Doe");
			dynamicProvider.CreateInstance(outType, instanceValues);

			string mapping = @"[{PropertyPath : 'firstName',Expression : 'FirstName'}, {PropertyPath : 'Id',Expression : 'Employee Id'}]";

			JsonMapping map = new JsonMapping(mapping);

			//The test will overwrite the existing account object with new values from the source entity.
			IEnumerable<ModelInstance> translatedInstances = translator.Translate(ModelContext.Current.GetModelType("Account"), outType, dynamicProvider.GetModelInstances(ModelContext.Current.GetModelType("TypeName")), map, (type, instance, id) =>
			{
				return type.Create(id);
			});

			Assert.AreEqual(translatedInstances.ElementAt(0)["firstName"], "John");
			Assert.AreEqual(translatedInstances.ElementAt(0)["lastName"], "User");
			Assert.AreEqual(translatedInstances.ElementAt(0)["accountNumber"], "1234");
		}

		[TestCleanup()]
		public void After()
		{
		}
	}
}
