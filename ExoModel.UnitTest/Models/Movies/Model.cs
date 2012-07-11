using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel.Json;
using System.IO;

namespace ExoModel.UnitTest.Models.Movies
{
	public class Model
	{
		/// <summary>
		/// Initializes the model context using json movie types and instances.
		/// </summary>
		public static void InitializeJsonModel()
		{
			// Load a json model with movie types
			var jsonModel = new JsonModel();

			// Initialize the model context
			ModelContext.Init(() =>
			{
				// Load the movie json types
				jsonModel.Load(File.ReadAllText(@"types.js"));
			},
			jsonModel);

			// Load the movie json instances
			jsonModel.Load(File.ReadAllText(@"instances.js"));
		}

		/// <summary>
		/// Initializes the model context by copying data from json movie instances
		/// into the strongly-typed movie test model types.
		/// </summary>
		public static void InitializeTestModel()
		{
			// Load a json model with movie types
			var jsonModel = new JsonModel();

			// Create a new test model
			var testModel = new TestModelTypeProvider("Test");

			// Initialize the model context
			ModelContext.Init(() => 
			{
				// Load the movie json types
				jsonModel.Load(File.ReadAllText(@"types.js"));
			}, 
			jsonModel, testModel);

			// Load the movie json data
			jsonModel.Load(File.ReadAllText(@"instances.js"));

			// Create instances in test model for each instance in the json model
			Dictionary<ModelInstance, ModelInstance> instances = new Dictionary<ModelInstance, ModelInstance>();
			foreach (var type in jsonModel.Types.Values)
			{
				foreach (var instance in jsonModel.GetInstances(type).Values)
				{
					instances[((IModelInstance)instance).Instance] = ((IModelInstance)Type.GetType("ExoModel.UnitTest.Models.Movies." + type.Name).GetConstructor(Type.EmptyTypes).Invoke(null)).Instance;
				}
			}

			// Set the properties in the test model for each json model instance
			foreach (var instance in instances)
			{
				var jsonInstance = instance.Key;
				var testInstance = instance.Value;

				foreach (var property in jsonInstance.Type.Properties)
				{
					if (property.IsStatic)
						continue;
					if (property is ModelValueProperty)
						testInstance.SetValue(property.Name, jsonInstance.GetValue(property.Name));
					else if (property.IsList)
					{
						var list = testInstance.GetList(property.Name);
						foreach (var child in jsonInstance.GetList(property.Name))
							list.Add(instances[child]);
					}
					else
					{
						var child = jsonInstance.GetReference(property.Name);
						testInstance.SetReference(property.Name, child != null ? instances[child] : null);
					}

				}

				// Save the instance
				testInstance.Save();
			}
		}
	}
}
