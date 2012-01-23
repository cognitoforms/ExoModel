using System;
using System.ComponentModel;

namespace ExoGraph
{
	/// <summary>
	/// Represents a property that exposes strongly-typed data as leaves of a graph hierarchy.
	/// </summary>
	public abstract class GraphValueProperty : GraphProperty
	{
		#region Constructors

		protected internal GraphValueProperty(GraphType declaringType, string name, bool isStatic, Type propertyType, TypeConverter converter, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
			: base(declaringType, name, isStatic, isList, isReadOnly, isPersisted,attributes)
		{
			this.PropertyType = propertyType;
			this.Converter = converter;
			this.AutoConvert = converter != null && converter.CanConvertTo(typeof(object));
		}

		#endregion

		#region Properties

		public Type PropertyType { get; private set; }

		public TypeConverter Converter { get; private set; }

		internal bool AutoConvert { get; private set; }

		#endregion
	}
}
