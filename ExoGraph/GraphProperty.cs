﻿using System.Collections.Generic;
using System;
using System.Runtime.Serialization;

namespace ExoGraph
{
	/// <summary>
	/// Represents a property on a type in a graph hierarchy.
	/// </summary>
	[Serializable]
	public abstract class GraphProperty : ISerializable
	{
		#region Fields

		string name;
		int index;
		bool isStatic;
		GraphType declaringType;
		List<GraphStep> observers = new List<GraphStep>();
		Attribute[] attributes;
		
		#endregion

		#region Constructors

		internal GraphProperty(GraphType declaringType, string name, int index, bool isStatic, Attribute[] attributes)
		{
			this.declaringType = declaringType;
			this.name = name;
			this.index = index;
			this.isStatic = isStatic;
			this.attributes = attributes;
		}

		#endregion

		#region Properties

		public string Name
		{
			get
			{
				return name;
			}
		}

		public int Index
		{
			get
			{
				return index;
			}
		}

		public bool IsStatic
		{
			get
			{
				return isStatic;
			}
		}

		public GraphType DeclaringType
		{
			get
			{
				return declaringType;
			}
		}

		internal List<GraphStep> Observers
		{
			get
			{
				return observers;
			}
		}

		#endregion

		#region Methods

		internal void OnChange(GraphInstance instance)
		{
			// Attempt to walk up the path to the root for each observer
			foreach (GraphStep observer in observers)
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
			foreach (Attribute attribute in attributes)
			{
				if (attribute is TAttribute)
					matches.Add((TAttribute)attribute);
			}

			return matches.ToArray();
		}

		public override string ToString()
		{
			return name;
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
				declaringType = (GraphType) info.GetValue("dt", typeof(GraphType));
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
