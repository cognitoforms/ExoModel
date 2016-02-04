using System;
using ExoModel.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExoModel.UnitTests.Models
{
	[TestClass]
	public abstract class TestModelBase
	{
		public TestContext TestContext { get; set; }

		protected JsonEntityContext Context { get; set; }

		private static string SharedKey { get; set; }

		[AssemblyInitialize]
		public static void InitializeSharedKey(TestContext testContext)
		{
			SharedKey = Guid.NewGuid().ToString();
		}

		[TestInitialize]
		public void Initialize()
		{
			Context = TestModel.Initialize(GetType(), TestContext, id: SharedKey);
		}

		[TestCleanup]
		public void DisposeContext()
		{
			ModelContext.Current = null;
		}
	}
}
