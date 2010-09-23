using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Objects;
using System.Data;
using System.Data.Objects.DataClasses;
using System.ComponentModel;
using System.Data.Metadata.Edm;
using System.Web;

namespace ExoGraph.EntityFramework
{
	public class EntityFrameworkGraphTypeProvider : ReflectionGraphTypeProvider
	{
		public EntityFrameworkGraphTypeProvider(Func<ObjectContext> createContext)
			: this("", createContext)
		{ }

		public EntityFrameworkGraphTypeProvider(string @namespace, Func<ObjectContext> createContext)
			: base(@namespace, GetEntityTypes(createContext()), null) 
		{
			this.CreateContext = createContext;
		}

		Func<ObjectContext> CreateContext { get; set; }

		static IEnumerable<Type> GetEntityTypes(ObjectContext context)
		{
			using (context)
			{
				foreach (Type type in context.GetType().Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GraphEntity))))
					yield return type;
			}
		}

		internal ObjectContext GetObjectContext()
		{
			Storage storage = GetStorage();
			if (storage.Context == null)
			{
				storage.Context = CreateContext();
				storage.Context.MetadataWorkspace.LoadFromAssembly(storage.Context.GetType().Assembly);
			}
			return storage.Context;
		}

		[ThreadStatic]
		static Storage context;

		/// <summary>
		/// Gets thread static or <see cref="HttpContext"/> storage for the <see cref="GraphContext"/>.
		/// </summary>
		/// <returns></returns>
		static Storage GetStorage()
		{
			HttpContext webCtx = HttpContext.Current;

			// If in a web request, store the reference in HttpContext
			if (webCtx != null)
			{
				Storage storage = (Storage)webCtx.Items[typeof(EntityFrameworkGraphTypeProvider)];

				if (storage == null)
					webCtx.Items[typeof(EntityFrameworkGraphTypeProvider)] = storage = new Storage();

				return storage;
			}

			// Otherwise, store the reference in a thread static variable
			else
			{
				if (context == null)
					context = new Storage();

				return context;
			}
		}

		protected override ReflectionGraphType CreateGraphType(string @namespace, Type type)
		{
			return new EntityGraphType(@namespace, type);
		}

		internal static GraphInstance CreateGraphInstance(object instance)
		{
			return ReflectionGraphTypeProvider.CreateGraphInstance(instance);
		}

		protected override GraphValueProperty CreateValueProperty(GraphType declaringType, System.Reflection.PropertyInfo property, string name, bool isStatic, Type propertyType, TypeConverter converter, bool isList, Attribute[] attributes)
		{
			// Do not include entity reference properties in the model
			if (property.PropertyType.IsSubclassOf(typeof(EntityReference)))
				return null;

			return base.CreateValueProperty(declaringType, property, name, isStatic, propertyType, converter, isList, attributes);
		}

		#region Storage

		/// <summary>
		/// Reference class used to provide storage for the context.
		/// </summary>
		class Storage
		{
			public ObjectContext Context { get; set; }
		}

		#endregion

		#region EntityGraphType

		[Serializable]
		public class EntityGraphType : ReflectionGraphType
		{
			string qualifiedEntitySetName;
			GraphValueProperty[] idProperties;

			protected internal EntityGraphType(string @namespace, Type type)
				: base(@namespace, type)
			{ }

			/// <summary>
			/// Performs initialization of the graph type outside of the constructor to avoid recursion deadlocks.
			/// </summary>
			protected override void OnInit()
			{
				base.OnInit();

				// Get the current object context
				ObjectContext context = GetObjectContext();

				// Find the base entity graph type
				GraphType baseType = this;
				while (baseType.BaseType is EntityGraphType)
					baseType = baseType.BaseType;

				// Determine the qualified entity set name
				// This assumes:
				//   1. only one entity container
				//   2. only one entity set for an given entity type
				//   3. only one entity type with a name that matches the graph type
				qualifiedEntitySetName = context.DefaultContainerName + "." +
					context.MetadataWorkspace.GetItems<EntityContainer>(DataSpace.CSpace)[0]
						.BaseEntitySets.First(s => s.ElementType.Name == baseType.Name).Name;

				// Get the value properties that comprise the identifier for the entity type
				idProperties = (
					from property in Properties
					where property is GraphValueProperty &&
						property.HasAttribute<EdmScalarPropertyAttribute>() &&
						property.GetAttributes<EdmScalarPropertyAttribute>()[0].EntityKeyProperty
					select property as GraphValueProperty
				).ToArray();
			}

			/// <summary>
			/// Gets or creates the object context for the current scope of work that corresponds to the 
			/// current <see cref="EntityGraphType"/>.
			/// </summary>
			/// <returns></returns>
			internal ObjectContext GetObjectContext()
			{
				return ((EntityFrameworkGraphTypeProvider)Provider).GetObjectContext();
			}

			protected override void SaveInstance(GraphInstance graphInstance)
			{
				GetObjectContext().SaveChanges(true);
			}

			protected override string GetId(object instance)
			{
				return ((GraphEntity)instance).Id;
			}

			internal void OnPropertyGet(GraphInstance instance, string property)
			{
				base.OnPropertyGet(instance, property);
			}

			internal void OnPropertyChanged(GraphInstance instance, string property, object oldValue, object newValue)
			{
				base.OnPropertyChanged(instance, property, oldValue, newValue);
			}

			public override GraphInstance GetGraphInstance(object instance)
			{
				return ((GraphEntity)instance).Instance;
			}

			protected override object GetInstance(string id)
			{
				// Get the current object context
				ObjectContext context = GetObjectContext();

				// Create a new instance if the id is null
				if (id == null)
					return context.GetType().GetMethod("CreateObject").MakeGenericMethod(UnderlyingType).Invoke(context, null);

				// Otherwise, load the existing instance

				// Split the id string into id tokens
				var tokens = id.Split(',');
				if (tokens.Length != idProperties.Length)
					throw new ArgumentException("The specified id, '" + id + "', does not have the correct number of key values.");

				// Create an entity key based on the specified id tokens
				var key = new EntityKey(qualifiedEntitySetName,
					idProperties.Select((property, index) => new EntityKeyMember(property.Name, TypeDescriptor.GetConverter(property.PropertyType).ConvertFromString(tokens[index]))));

				// Attempt to create the entity using the key
				object instance;
				context.TryGetObjectByKey(key, out instance);
				return instance as GraphEntity;
			}

			protected override void DeleteInstance(GraphInstance graphInstance)
			{
				throw new NotImplementedException();
			}
		}
		#endregion
	}
}
