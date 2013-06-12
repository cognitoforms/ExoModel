using System.Collections.Generic;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace ExoModel
{
	/// <summary>
	/// Represents a property on a type in a model hierarchy.
	/// </summary>
	[Serializable]
	public abstract class ModelProperty : ISerializable
	{
		#region Fields

		static Regex labelRegex = new Regex(@"(^[a-z]+|[A-Z]{2,}(?=[A-Z][a-z]|$)|[A-Z][a-z]*)", RegexOptions.Singleline | RegexOptions.Compiled);

		#endregion

		#region Constructors

		internal ModelProperty(ModelType declaringType, string name, string label, string helptext, string format, bool isStatic, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
		{
			this.DeclaringType = declaringType;
			this.Name = name;
			this.Label = label ?? labelRegex.Replace(Name, " $1").Substring(1);
			this.HelpText = helptext;
			this.Format = format;
			this.IsStatic = isStatic;
			this.IsList = isList;
			this.IsReadOnly = isReadOnly;
			this.IsPersisted = isPersisted;
			this.Attributes = attributes ?? new Attribute[0];
			this.Observers = new List<ModelStep>();
		}

		#endregion

		#region Properties

		public string Name { get; private set; }

		public virtual string Label { get; private set; }

		public virtual string HelpText { get; private set; }

		public virtual string Format { get; private set; }

		public int Index { get; internal set; }

		public bool IsStatic { get; private set; }

		public bool IsList { get; private set; }

		public bool IsReadOnly { get; private set; }

		public bool IsPersisted { get; private set; }

		public Attribute[] Attributes { get; private set; }

		public ModelType DeclaringType { get; private set; }

		internal List<ModelStep> Observers { get; private set; }

		#endregion

		#region Methods
		
		public void OnPropertyChanged(ModelInstance instance, object oldValue, object newValue)
		{
			instance.Type.OnPropertyChanged(instance, this, oldValue, newValue);
		}

		public void NotifyPathChange(ModelInstance instance)
		{
			// Attempt to walk up the path to the root for each observer
			foreach (ModelStep observer in Observers)
				observer.Notify(instance);
		}

		/// <summary>
		/// Indicates whether the current property has one or more attributes of the specified type.
		/// </summary>
		/// <typeparam name="TAttribute"></typeparam>
		/// <returns></returns>
		public bool HasAttribute<TAttribute>()
			where TAttribute : Attribute
		{
			return GetAttributes<TAttribute>().Length > 0;
		}

		/// <summary>
		/// Returns an array of attributes defined on the current property.
		/// </summary>
		/// <typeparam name="TAttribute"></typeparam>
		/// <returns></returns>
		public TAttribute[] GetAttributes<TAttribute>()
			where TAttribute : Attribute
		{
			List<TAttribute> matches = new List<TAttribute>();

			// Find matching attributes on the current type
			foreach (Attribute attribute in Attributes)
			{
				if (attribute is TAttribute)
					matches.Add((TAttribute)attribute);
			}

			return matches.ToArray();
		}

		/// <summary>
		/// Returns true if the property has any observers.
		/// </summary>
		/// <returns></returns>
		public bool HasObservers()
		{
			return Observers.Any();
		}

		/// <summary>
		/// Returns true if the property has any observers.
		/// </summary>
		/// <param name="instance">Specific instance to check for.</param>
		/// <returns></returns>
		public bool HasObservers(ModelInstance instance)
		{
			return Observers.Any(step => step.IsReferencedToRoot(instance, true));
		}

		/// <summary>
		/// Returns the name of the property.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Name;
		}

		/// <summary>
		/// Gets the value of the property on the specified instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected internal abstract object GetValue(object instance);

		/// <summary>
		/// Sets the value of the property on the specified instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="value"></param>
		protected internal abstract void SetValue(object instance, object value);

		/// <summary>
		/// Gets the formatted value of the current property for the specified instance.
		/// </summary>
		/// <param name="instance">The specific <see cref="ModelInstance"/></param>
		/// <param name="format">The optional format specifier to use to format the value, or null to use the default property format</param>
		/// <returns>The formatted value of the property</returns>
		internal abstract string GetFormattedValue(ModelInstance instance, string format);

		#endregion

		#region ISerializable Members

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.SetType(typeof(Serialized));
			info.AddValue("dt", DeclaringType);
			info.AddValue("p", Name);
		}

		[Serializable]
		class Serialized : ISerializable, IObjectReference
		{
			ModelType declaringType;
			string name;

			#region ISerializable Members
			public Serialized(SerializationInfo info, StreamingContext context)
			{
				declaringType = (ModelType)info.GetValue("dt", typeof(ModelType));
				name = info.GetString("p");
			}

			void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
			{
				throw new NotImplementedException("this code should never run");
			}
			#endregion

			#region IObjectReference Members
			public object GetRealObject(StreamingContext context)
			{
				return declaringType.Properties[name];
			}
			#endregion
		}


		#endregion
	}
}

