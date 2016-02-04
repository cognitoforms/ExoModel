using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ExoModel.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExoModel.UnitTests.Models
{
	public static class TestModel
	{
		public static JsonEntityContext Initialize(Type testType, TestContext testContext, Action<ModelContext> onContextCreated = null, string id = null)
		{
			var modelId = id ?? Guid.NewGuid().ToString();

			// ReSharper disable EmptyGeneralCatchClause

			try
			{
				ModelContext.Current = null;
			}
			catch
			{
			}

			// ReSharper restore EmptyGeneralCatchClause

			string modelName = null;

			var storageMode = TestModelStorageMode.Unspecified;

			var testTypeModelAttribute = (TestModelAttribute)testType.GetCustomAttributes(typeof(TestModelAttribute), false).SingleOrDefault();
			if (testTypeModelAttribute != null)
			{
				modelName = testTypeModelAttribute.Name;

				if (testTypeModelAttribute.StorageMode != TestModelStorageMode.Unspecified)
					storageMode = testTypeModelAttribute.StorageMode;
			}

			var testMethod = testType.GetMethod(testContext.TestName, BindingFlags.Instance | BindingFlags.Public);
			var testMethodModelAttribute = (TestModelAttribute)testMethod.GetCustomAttributes(typeof(TestModelAttribute), false).SingleOrDefault();

			if (testMethodModelAttribute != null)
			{
				modelName = testMethodModelAttribute.Name;

				if (testMethodModelAttribute.StorageMode != TestModelStorageMode.Unspecified)
					storageMode = testMethodModelAttribute.StorageMode;
			}

			if (modelName == null)
				return null;

			string storagePath;

			var assembly = testType.Assembly;
			var projectDir = GetProjectDirectory(assembly);
			var permanentStoragePath = Path.Combine(projectDir, "Models\\" + modelName + "\\DataFiles");

			if (storageMode == TestModelStorageMode.Temporary)
			{
				var tempPath = Path.GetTempPath();

				storagePath = Path.Combine(tempPath, "ModelsTemp\\" + modelId + "\\" + testType.Name + "_" + testContext.TestName + "\\" + modelName);

				Directory.CreateDirectory(storagePath);

				// Seed temporary files from permanent files.
				foreach (var permanentDataFilePath in Directory.GetFiles(permanentStoragePath, "*.json"))
				{
					var permanentDataFileName = Path.GetFileName(permanentDataFilePath);
					if (permanentDataFileName == null)
						continue;

					File.Copy(permanentDataFilePath, Path.Combine(storagePath, permanentDataFileName));
				}
			}
			else
			{
				storagePath = permanentStoragePath;
			}

			return JsonModel.Initialize(assembly, assembly.GetName().Name + ".Models." + modelName, storagePath, onContextCreated);
		}

		private static String GetProjectDirectory(Assembly assembly = null)
		{
			if (assembly == null)
				assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

			string projectDirectory;

			//var webCtx = HttpContext.Current;
			//if (webCtx != null)
			//	projectDirectory = webCtx.Server.MapPath("~");
			//else
			//{

			var assemblyDirectory = Path.GetDirectoryName(new Uri(assembly.CodeBase).AbsolutePath);
			if (assemblyDirectory == null)
				throw new Exception("Could not determine the location of assembly '" + assembly.GetName().Name + "'.");

			var assemblyDirectoryName = Path.GetFileName(assemblyDirectory);
			var assemblyParentDirectory = Path.GetDirectoryName(assemblyDirectory);
			var assemblyParentDirectoryName = Path.GetFileName(assemblyParentDirectory);

			if (assemblyParentDirectoryName == "bin")
			{
				// i.e. "\bin\Debug\*.dll"
				projectDirectory = Path.GetDirectoryName(assemblyParentDirectory);
			}
			else if (assemblyDirectoryName == "bin")
			{
				// i.e. "\bin\*.dll"
				projectDirectory = assemblyParentDirectory;
			}
			else
			{
				var assemblyGrandparentDirectory = Path.GetDirectoryName(assemblyParentDirectory);
				var assemblyGrandparentDirectoryName = Path.GetFileName(assemblyGrandparentDirectory);

				if (assemblyDirectoryName == "Out" && assemblyGrandparentDirectoryName == "TestResults")
				{
					// \TestResults\Deploy_username yyyy-MM-dd hh_mm_ss\Out\*.dll
					var appDirectory = Path.GetDirectoryName(assemblyGrandparentDirectory);
					if (appDirectory == null)
						throw new Exception("Found test files in unexpected location '" + assemblyDirectory + "'.");

					projectDirectory = Path.Combine(appDirectory, assembly.GetName().Name);
				}
				else
					throw new Exception("Executing assembly in unexpected location '" + assemblyDirectory + "'.");
			}

			if (projectDirectory == null)
				throw new Exception("Could not determine location of project for assembly '" + assembly.GetName().Name + "' executing from '" + assemblyDirectory + "'.");

			//}

			return projectDirectory;
		}
	}
}
