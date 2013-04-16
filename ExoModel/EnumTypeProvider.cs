using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace ExoModel
{
	public class EnumTypeProvider : IModelTypeProvider
	{
		#region Fields

		Dictionary<string, Type> typeNames = new Dictionary<string, Type>();
		HashSet<Type> supportedTypes = new HashSet<Type>();
		string @namespace;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="EnumTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create model types from</param>
		public EnumTypeProvider(params Assembly[] assemblies)
			: this("", assemblies)
		{ }

		/// <summary>
		/// Creates a new <see cref="EnumTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create model types from</param>
		public EnumTypeProvider(string @namespace, params Assembly[] assemblies)
			: this(@namespace,
				assemblies
				.SelectMany(a => a.GetTypes())
				.Where(t => t.IsEnum))
		{ }

		/// <summary>
		/// Creates a new <see cref="EnumTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create model types from</param>
		public EnumTypeProvider(IEnumerable<Type> types)
			: this("", types)
		{ }

		/// <summary>
		/// Creates a new <see cref="EnumTypeProvider"/> based on the specified types.
		/// </summary>
		/// <param name="types">The types to create model types from</param>
		public EnumTypeProvider(string @namespace, IEnumerable<Type> types)
		{
			// The list of types cannot be null
			if (types == null)
				throw new ArgumentNullException("types");

			this.@namespace = string.IsNullOrEmpty(@namespace) ? string.Empty : @namespace + ".";

			// Create dictionaries of type names and valid supported types to introspect
			foreach (Type type in types)
			{
				if (!type.IsEnum)
					throw new ArgumentException("Only enumeration types are supported by the EnumTypeProvider.");

				typeNames.Add(this.@namespace + type.Name, type);

				if (!supportedTypes.Contains(type))
					supportedTypes.Add(type);
			}
		}

		#endregion

		#region IModelTypeProvider

		bool IModelTypeProvider.IsCachable { get { return true; } }

		string IModelTypeProvider.Namespace { get { return ""; } }

		/// <summary>
		/// Gets the unique name of the <see cref="ModelType"/> for the specified model object instance.
		/// </summary>
		/// <param name="instance">The actual model object instance</param>
		/// <returns>The unique name of the model type for the instance if it is a valid model type, otherwise null</returns>
		string IModelTypeProvider.GetModelTypeName(object instance)
		{
			var type = instance.GetType();
			return type.IsEnum && supportedTypes.Contains(type) ? @namespace + type.Name : null;
		}

		/// <summary>
		/// Gets the <see cref="ModelType"/> that corresponds to the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		string IModelTypeProvider.GetModelTypeName(Type type)
		{
			return type.IsEnum && supportedTypes.Contains(type) ? @namespace + type.Name : null;
		}

		/// <summary>
		/// Creates a <see cref="ModelType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		ModelType IModelTypeProvider.CreateModelType(string typeName)
		{
			Type type;

			// Get the type that corresponds to the specified type name
			if (!typeNames.TryGetValue(typeName, out type))
				return null;

			// Get the default reference format for the type
			string format = "[DisplayName]";

			// Create the new model type
			return new EnumModelType(@namespace, type, format);
		}

		#endregion

		#region EnumModelType

		class EnumModelType : ModelType
		{
			Dictionary<string, Enum> values;
			Dictionary<Enum, ModelInstance> instances;

			protected internal EnumModelType(string @namespace, Type type, string format)
				: base(@namespace + type.Name, type.AssemblyQualifiedName, null, "Enum", format, type.GetCustomAttributes(false).Cast<Attribute>().ToArray())
            {
                this.EnumType = type;
				values = Enum.GetValues(type).OfType<Enum>().ToDictionary(e => (Convert.ToInt32(e)).ToString());
				instances = Enum.GetValues(type).OfType<Enum>().Select(e => new { Enum = e, Instance = new ModelInstance(e) }).ToDictionary(e => e.Enum, e => e.Instance);
            }

			public Type EnumType { get; private set; }

			protected internal override bool IsCached(object instance)
			{
				return true;
			}

			protected internal override void EnterLock(object instance, out bool acquired)
			{
				// locking is not really implemented
				acquired = true;
			}

			protected internal override void ExitLock(object instance, bool acquired)
			{
			}

			protected internal override void OnInit()
			{
				// Id
				AddProperty(new EnumValueProperty<int>(this, "Id", e => Convert.ToInt32(e)));

				// Name
				AddProperty(new EnumValueProperty<string>(this, "Name", e => e.ToString()));

				// Display Name
				AddProperty(new EnumValueProperty<string>(this, "DisplayName", e => e.GetDisplayName()));

				// All
				AddProperty(new EnumAllProperty(this));
			}

			protected internal override void SaveInstance(ModelInstance modelInstance)
			{
				throw new NotSupportedException();
			}

			public override ModelInstance GetModelInstance(object instance)
			{
				return instances[(Enum)instance];
			}

			protected internal override string GetId(object instance)
			{
				return ((int)instance).ToString();
			}

			protected internal override object GetInstance(string id)
            {
				return values[id];
            }

			protected internal override bool GetIsModified(object instance)
			{
				return false;
			}

			protected internal override bool GetIsDeleted(object instance)
			{
				return false;
			}

			protected internal override bool GetIsPendingDelete(object instance)
			{
				return false;
			}

			protected internal override void SetIsPendingDelete(object instance, bool isPendingDelete)
			{
				throw new NotSupportedException();
			}

			#region EnumValueProperty

			[Serializable]
			public class EnumAllProperty : ModelReferenceProperty
			{
				object values;

				protected internal EnumAllProperty(EnumModelType declaringType)
					: base(declaringType, "All", null, null, null, true, declaringType, true, true, false, new Attribute[] { })
				{ }

				protected internal override object GetValue(object instance)
				{
					if (values == null)
						values = ((EnumModelType)this.DeclaringType).values.Values.ToArray();

					return values;
				}

				protected internal override void SetValue(object instance, object value)
				{
					throw new NotSupportedException();
				}
			}

			#endregion
		}

		#endregion

		#region EnumValueProperty

		[Serializable]
		public class EnumValueProperty<T> : ModelValueProperty
		{
			Func<Enum, T> getValue;

			protected internal EnumValueProperty(ModelType declaringType, string name, Func<Enum, T> getValue)
				: base(declaringType, name, null, null, null, false, typeof(T), null, false, true, false, new Attribute[] { })
			{
				this.getValue = getValue;
			}

			protected internal override object GetValue(object instance)
			{
				return getValue((Enum)instance);
			}

			protected internal override void SetValue(object instance, object value)
			{
				throw new NotSupportedException();
			}
		}

		#endregion
	}

	public static class EnumExtensions
	{
		static readonly Regex nameRegex = new Regex(@"(^[a-z]+|[A-Z]{2,}(?=[A-Z][a-z]|$)|[A-Z][a-z]*)", RegexOptions.Singleline | RegexOptions.Compiled);

		public static string GetDisplayName(this Enum value)
		{
			var displayAttribute = value.GetType().GetField(value.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();

			return displayAttribute == null ? nameRegex.Replace(value.ToString(), " $1").Substring(1) : ((DescriptionAttribute)displayAttribute).Description;
		}
	}
}
