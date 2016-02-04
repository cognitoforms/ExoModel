using System;

namespace ExoModel.UnitTests.Models
{
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class TestModelAttribute : Attribute
	{
		public string Name { get; set; }

		public TestModelStorageMode StorageMode { get; set; }
	}
}
