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
	public class RowInstance : IModelInstance
	{
		ModelInstance instance;
		object[] instanceProperties;

		internal RowInstance(ModelType type)
			: this(type, null)
		{ }

		internal RowInstance(ModelType type, string id)
		{
			this.Type = type;
			this.Id = id;
			this.instance = new ModelInstance(this);
			instanceProperties = new object[Type.Properties.Count];
		}

		ModelInstance IModelInstance.Instance
		{
			get { return instance; }
		}

		internal string Id { get; private set; }

		internal ModelType Type { get; private set; }

		public object this[string property]
		{
			get
			{
				return this[Type.Properties[property]];
			}
			set
			{
				this[instance.Type.Properties[property]] = value;
			}
		}

		public object this[ModelProperty property]
		{
			get
			{
				var value = instanceProperties[property.Index];
				if (value == null && property is ModelReferenceProperty && property.IsList)
					instanceProperties[property.Index] = value = new ObservableCollection<RowInstance>();
				return value;
			}
			set
			{
				instanceProperties[property.Index] = value;
			}
		}
	}
}
