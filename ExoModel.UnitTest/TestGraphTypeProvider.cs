using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoGraph;
using System.Reflection;

namespace ExoGraph.UnitTest
{
	/// <summary>
	/// Test subclass of <see cref="ReflectionGraphTypeProvider"/> that provides a limited implementation to support unit testing.
	/// </summary>
	public class TestGraphTypeProvider : ReflectionGraphTypeProvider
	{
		Dictionary<Type, List<TestEntity>> entities = new Dictionary<Type, List<TestEntity>>();

		public TestGraphTypeProvider()
			: this(Assembly.GetExecutingAssembly())
		{ }

		public TestGraphTypeProvider(Assembly assembly)
			: base(assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(TestEntity))))
		{ }

		protected override ReflectionGraphTypeProvider.ReflectionGraphType CreateGraphType(string @namespace, Type type, string format)
		{
			return new TestGraphType(@namespace, type, format);
		}

		class TestGraphType : ReflectionGraphType
		{
			internal TestGraphType(string @namespace, Type type, string format)
				: base(@namespace, type, null, format)
			{ }

			/// <summary>
			/// Saves the specified instance and all related instances.
			/// </summary>
			/// <param name="instance"></param>
			protected override void SaveInstance(GraphInstance instance)
			{
				Save(instance, new HashSet<GraphInstance>());
			}

			new void Save(GraphInstance instance, HashSet<GraphInstance> saved)
			{
				var entity = instance.Instance as TestEntity;

				// Exit immediately if the instance is not an entity or has already been saved
				if (entity == null || saved.Contains(instance))
					return;

				// Track that the instance is being saved
				saved.Add(instance);

				// Save new instances
				if (entity.Id == null)
				{
					List<TestEntity> family;
					if (!((TestGraphTypeProvider)Provider).entities.TryGetValue(entity.GetType(), out family))
					{
						((TestGraphTypeProvider)Provider).entities[entity.GetType()] = family = new List<TestEntity>();
						family.Add(null);
					}
					entity.Id = family.Count;
					family.Add(entity);
				}

				// Recursively save child instances
				foreach (var child in instance.Type.Properties
					.OfType<GraphReferenceProperty>()
					.SelectMany(p => p.GetInstances(instance)))
					Save(child, saved);

				// Notify the context that the instance has been saved
				OnSave(instance);
			}

			/// <summary>
			/// Gets the string identifier of the test entity.
			/// </summary>
			/// <param name="instance"></param>
			/// <returns></returns>
			protected override string GetId(object instance)
			{
				var entity = instance as TestEntity;
				return entity.Id != null ? entity.Id.ToString() : null;
			}

			protected override object GetInstance(string id)
			{
				if (id == null)
					return Activator.CreateInstance(UnderlyingType);
				else
				{
					int index;
					if (!Int32.TryParse(id, out index))
						throw new ArgumentException("Invalid non-integer identifier.");
					List<TestEntity> family;
					if (((TestGraphTypeProvider)Provider).entities.TryGetValue(UnderlyingType, out family))
					{
						if (index <= 0 || index >= family.Count)
							throw new ArgumentException("No entity exists with the specified id.");
						return family[index];
					}
					else
						throw new ArgumentException("No saved entities of the specified type exist.");
				}
			}

			protected override bool GetIsModified(object instance)
			{
				throw new NotImplementedException();
			}

			protected override bool GetIsDeleted(object instance)
			{
				throw new NotImplementedException();
			}

			protected override bool GetIsPendingDelete(object instance)
			{
				throw new NotImplementedException();
			}

			protected override void SetIsPendingDelete(object instance, bool isPendingDelete)
			{
				throw new NotImplementedException();
			}
		}
	}
}
