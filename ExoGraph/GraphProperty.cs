using System.Collections.Generic;
using System;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace ExoGraph
{
	/// <summary>
	/// Represents a property on a type in a graph hierarchy.
	/// </summary>
	[Serializable]
	[DataContract]
	public abstract class GraphProperty : ISerializable
	{
		#region Fields

		static Regex labelRegex = new Regex(@"(^[a-z]+|[A-Z]{2,}(?=[A-Z][a-z]|$)|[A-Z][a-z]*)", RegexOptions.Singleline | RegexOptions.Compiled);

		#endregion

		#region Constructors

		internal GraphProperty(GraphType declaringType, string name, bool isStatic, bool isList, bool isReadOnly, Attribute[] attributes)
		{
			this.DeclaringType = declaringType;
			this.Name = name;
			this.IsStatic = isStatic;
			this.IsList = isList;
			this.IsReadOnly = isReadOnly;
			this.Attributes = attributes;
			this.Observers = new List<GraphStep>();
		}

		#endregion

		#region Properties

		[DataMember(Name = "name")]
		public string Name { get; private set; }

		public string Label
		{
			get
			{
				return GetLabel();
			}
		}

		public int Index { get; internal set; }

		public bool IsStatic { get; private set; }

		public bool IsList { get; private set; }

		public bool IsReadOnly { get; private set; }

		public Attribute[] Attributes { get; private set; }

		public GraphType DeclaringType { get; private set; }

		internal List<GraphStep> Observers { get; private set; }

		#endregion

		#region Methods
		
		public void OnPropertyChanged(GraphInstance instance, object oldValue, object newValue)
		{
			instance.Type.OnPropertyChanged(instance, this, oldValue, newValue);
		}

		public void NotifyPathChange(GraphInstance instance)
		{
			// Attempt to walk up the path to the root for each observer
			foreach (GraphStep observer in Observers)
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
		/// Determines the appropriate label for use in a user interface to display for the property.
		/// </summary>
		/// <returns></returns>
		protected virtual string GetLabel()
		{
			return labelRegex.Replace(Name, " $1").Substring(1);
		}

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
			GraphType declaringType;
			string name;

			#region ISerializable Members
			public Serialized(SerializationInfo info, StreamingContext context)
			{
				declaringType = (GraphType)info.GetValue("dt", typeof(GraphType));
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

