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
		object[] values;

		internal RowInstance(RowModelType type)
			: base(type, "")
		{
			this.values = new object[type.Properties.Count];

			// Initialize list properties
			foreach (var property in type.Properties)
			{
				if (property is ModelReferenceProperty && ((ModelReferenceProperty)property).IsList)
					this.values[property.Index] = new RowInstanceList(this);
			}
		}

		class RowInstanceList : System.Collections.ObjectModel.Collection<RowInstance>
		{
			RowInstance parent;
			internal RowInstanceList(RowInstance parent)
			{
				this.parent = parent;
			}

			protected override void InsertItem(int index, RowInstance item)
			{
				item.Index = index;
				base.InsertItem(index, item);
				item.Parent = parent;
			}
		}

		public RowInstance Parent { get; private set; }

		public int Index { get; private set; }

		ModelInstance IModelInstance.Instance
		{
			get { return this; }
		}

		internal object this[int index]
		{
			get { return values[index]; }
			set { values[index] = value;  }
		}

		internal IEnumerable<string> Values
		{
			get
			{
				return Type.Properties.OfType<IColumn>().OrderBy(p => p.Sequence).OfType<ModelValueProperty>().Select(p => this.GetFormattedValue(p));
			}
		}

		public override string ToString()
		{
			return Id;
		}
	}
}
