using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;
using System.Reflection;
using System.ComponentModel.DataAnnotations;

namespace ExoModel.UnitTest
{
	/// <summary>
	/// Test subclass of <see cref="ReflectionModelTypeProvider"/> that provides a limited implementation to support unit testing.
	/// </summary>
	public class TestModelTypeProvider : ReflectionModelTypeProvider
	{
		#region Fields

		[ThreadStatic]
		static WeakReference current;

		Dictionary<Type, List<TestEntity>> entities = new Dictionary<Type, List<TestEntity>>();

		#endregion

		#region Constructors

		public TestModelTypeProvider()
			: this("", Assembly.GetExecutingAssembly())
		{ }

		public TestModelTypeProvider(string @namespace)
			: this(@namespace, Assembly.GetExecutingAssembly())
		{
			Current = this;
		}

		public TestModelTypeProvider(Assembly assembly)
			: this("", assembly)
		{
			Current = this;
		}

		public TestModelTypeProvider(string @namespace, Assembly assembly)
			: base(@namespace, assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(TestEntity))))
		{
			Current = this;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the current <see cref="TestModelTypeProvider"/> for the current thread of execution.
		/// </summary>
		internal static TestModelTypeProvider Current
		{
			get
			{
				return current != null && current.IsAlive ? (TestModelTypeProvider)current.Target : null;
			}
			set
			{
				if (value == null)
					current = null;
				else
					current = new WeakReference(value);
			}
		}

		#endregion

		#region Methods

		public List<TestEntity> GetEntities(Type entityType)
		{
			return entities.ContainsKey(entityType) ? entities[entityType] : new List<TestEntity>();
		}

		protected override ReflectionModelTypeProvider.ReflectionModelType CreateModelType(string @namespace, Type type, string format)
		{
			return new TestModelType(@namespace, type, format);
		}

		protected override ModelValueProperty CreateValueProperty(ModelType declaringType, PropertyInfo property, string name, string label, string format, bool isStatic, Type propertyType, System.ComponentModel.TypeConverter converter, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
		{
			format = format ?? attributes.OfType<DisplayFormatAttribute>().Select(f => f.DataFormatString).FirstOrDefault();
			return base.CreateValueProperty(declaringType, property, name, label, format, isStatic, propertyType, converter, isList, isReadOnly, isPersisted, attributes);
		}

		#endregion

		#region TestModelType

		class TestModelType : ReflectionModelType
		{
			internal TestModelType(string @namespace, Type type, string format)
				: base(@namespace, type, null, format)
			{ }

			/// <summary>
			/// Saves the specified instance and all related instances.
			/// </summary>
			/// <param name="instance"></param>
			protected override void SaveInstance(ModelInstance instance)
			{
				Save(instance, new HashSet<ModelInstance>());
			}

			new void Save(ModelInstance instance, HashSet<ModelInstance> saved)
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
					if (!((TestModelTypeProvider)Provider).entities.TryGetValue(entity.GetType(), out family))
					{
						((TestModelTypeProvider)Provider).entities[entity.GetType()] = family = new List<TestEntity>();
						family.Add(null);
					}
					entity.Id = family.Count;
					family.Add(entity);
				}

				// Recursively save child instances
				foreach (var child in instance.Type.Properties
					.OfType<ModelReferenceProperty>()
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
					if (((TestModelTypeProvider)Provider).entities.TryGetValue(UnderlyingType, out family))
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

		#endregion
	}
}
