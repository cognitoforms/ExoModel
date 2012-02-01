using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using System.Web.Script.Serialization;
using System.Collections.ObjectModel;
using System.Collections;

namespace ExoGraph.UnitTest.JsonModel
{
	/// <summary>
	/// Custom <see cref="IGraphTypeProvider"/> implementation that supports defining a graph
	/// via JSON type metadata and JSON instance data using dynamic instances.
	/// </summary>
	public class JsonGraphTypeProvider : IGraphTypeProvider
	{
		public JsonGraphTypeProvider()
		{
			this.Graph = new JsonModel();
		}

		public JsonGraphTypeProvider(string json)
		{
			this.Graph = new JsonModel(json);
		}

		public JsonGraphTypeProvider Load(string json)
		{
			Graph.Load(json);
			return this;
		}

		public JsonModel Graph { get; private set; }

		#region IGraphTypeProvider

		string IGraphTypeProvider.GetGraphTypeName(object instance)
		{
			if (instance is JsonInstance)
				return ((IGraphInstance)instance).Instance.Type.Name;
			return null;
		}

		string IGraphTypeProvider.GetGraphTypeName(Type type)
		{
			return null;
		}

		GraphType IGraphTypeProvider.CreateGraphType(string typeName)
		{
			GraphType type;
			if (Graph.Types.TryGetValue(typeName, out type))
				return type;
			return null;
		}

		#endregion

		#region JsonGraphType

		internal class JsonGraphType : GraphType
		{
			internal JsonGraphType(string name, string qualifiedName, GraphType baseType, string scope, string format, Attribute[] attributes)
				: base(name, qualifiedName, baseType, scope, format, attributes)
			{ }

			internal void AddProperties(IEnumerable<GraphProperty> properties)
			{
				foreach (var property in properties)
					AddProperty(property);
			}

			protected override void OnInit()
			{ }

			protected override System.Collections.IList ConvertToList(GraphReferenceProperty property, object list)
			{
				return (IList)list;
			}

			protected override void SaveInstance(GraphInstance graphInstance)
			{
				throw new NotImplementedException();
			}

			public override GraphInstance GetGraphInstance(object instance)
			{
				return ((IGraphInstance)instance).Instance;
			}

			protected override string GetId(object instance)
			{
				return ((JsonInstance)instance).Id;
			}

			protected override object GetInstance(string id)
			{
				return ((JsonGraphTypeProvider)Provider).Graph.GetInstance(this, id);
			}

			protected override bool GetIsModified(object instance)
			{
				throw new NotImplementedException();
			}

			protected override bool GetIsDeleted(object instance)
			{
				throw new NotImplementedException();
			}

			protected override bool GetIsPendingDelete(object instance)
			{
				throw new NotImplementedException();
			}

			protected override void SetIsPendingDelete(object instance, bool isPendingDelete)
			{
				throw new NotImplementedException();
			}
		}

		#endregion

		#region JsonGraphValueProperty

		internal class JsonGraphValueProperty : GraphValueProperty
		{
			internal JsonGraphValueProperty(GraphType declaringType, string name, string label, string format, bool isStatic, Type propertyType, bool isList, bool isReadOnly, bool isPersisted)
				: base(declaringType, name, label, format, isStatic, propertyType, null, isList, isReadOnly, isPersisted, null)
			{ }

			protected override object GetValue(object instance)
			{
				throw new NotImplementedException();
			}

			protected override void SetValue(object instance, object value)
			{
				throw new NotImplementedException();
			}
		}

		#endregion

		#region JsonGraphReferenceProperty

		internal class JsonGraphReferenceProperty : GraphReferenceProperty
		{
			internal JsonGraphReferenceProperty(GraphType declaringType, string name, string label, string format, bool isStatic, GraphType propertyType, bool isList, bool isReadOnly, bool isPersisted)
				: base(declaringType, name, label, format, isStatic, propertyType, isList, isReadOnly, isPersisted, null)
			{ }

			protected override object GetValue(object instance)
			{
				throw new NotImplementedException();
			}

			protected override void SetValue(object instance, object value)
			{
				throw new NotImplementedException();
			}
		}

		#endregion
	}
}
