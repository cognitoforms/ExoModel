using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using ExoGraph;
using System.Collections.ObjectModel;

namespace ExoGraph.UnitTest.JsonModel
{
	/// <summary>
	/// Represents a concrete dynamic graph instance, relying on the <see cref="GraphInstance"/>
	/// metadata to expose dynamic properties and behavior.
	/// </summary>
	public class JsonInstance : DynamicObject, IGraphInstance
	{
		GraphInstance instance;
		object[] instanceProperties;

		internal JsonInstance(GraphType type)
			: this(type, null)
		{ }

		internal JsonInstance(GraphType type, string id)
		{
			this.Type = type;
			this.Id = id;
			this.instance = new GraphInstance(this);
			instanceProperties = new object[Type.Properties.Count];
		}

		GraphInstance IGraphInstance.Instance
		{
			get { return instance; }
		}

		internal string Id { get; private set; }

		internal GraphType Type { get; private set; }

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

		public object this[GraphProperty property]
		{
			get
			{
				var value = instanceProperties[property.Index];
				if (value == null && property is GraphReferenceProperty && property.IsList)
					instanceProperties[property.Index] = value = new ObservableCollection<JsonInstance>();
				return value;
			}
			set
			{
				instanceProperties[property.Index] = value;
			}
		}
	}
}
