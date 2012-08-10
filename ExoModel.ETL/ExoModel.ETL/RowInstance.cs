using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using ExoModel;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace ExoModel.ETL
{
	/// <summary>
	/// Represents a concrete dynamic model instance, relying on the <see cref="ModelInstance"/>
	/// metadata to expose dynamic properties and behavior.
	/// </summary>
	public class RowInstance : ModelInstance, IModelInstance
	{
		string[] values;

		internal RowInstance(ModelType type)
			: this(type, new string[type.Properties.Count])
		{ }

		internal RowInstance(ModelType type, string[] values)
			: base(type, "")
		{
			this.values = values;
		}

		ModelInstance IModelInstance.Instance
		{
			get { return this; }
		}

		public new object this[ModelProperty property]
		{
			get
			{
				return values[property.Index];
			}
			set
			{
				values[property.Index] = value as string;
			}
		}
	}
}
