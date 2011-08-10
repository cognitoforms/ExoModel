using System;
using System.Collections.Generic;
namespace ExoGraph
{
	/// <summary>
	/// Represents a property that associates two types in a graph hierarchy.
	/// </summary>
	[Serializable]
	public abstract class GraphReferenceProperty : GraphProperty
	{
		#region Constructors

		protected internal GraphReferenceProperty(GraphType declaringType, string name, bool isStatic, GraphType propertyType, bool isList, bool isReadOnly, Attribute[] attributes)
			: base(declaringType, name, isStatic, isList, isReadOnly, attributes)
		{
			this.PropertyType = propertyType;
		}

		#endregion

		#region Properties

		public GraphType PropertyType { get; private set; }

		#endregion

		#region Methods

		/// <summary>
		/// Enumerates over the set of instances represented by the current step.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public IEnumerable<GraphInstance> GetInstances(GraphInstance instance)
		{
			// Exit immediately if the property is not valid for the specified instance
			if (!DeclaringType.IsInstanceOfType(instance))
				throw new ArgumentException("The current property is not valid for the specified instance.");

			// Return each instance exposed by a list property
			if (IsList)
			{
				GraphInstanceList children = instance.GetList(this);
				if (children != null)
				{
					foreach (GraphInstance child in children)
						yield return child;
				}
			}

			// Return the instance exposed by a reference property
			else
			{
				GraphInstance child = instance.GetReference(this);
				if (child != null)
					yield return child;
			}
		}

		#endregion
	}
}
