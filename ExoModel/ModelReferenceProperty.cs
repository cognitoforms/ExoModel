using System;
using System.Collections.Generic;
using System.Linq;

namespace ExoModel
{
	/// <summary>
	/// Represents a property that associates two types in a model hierarchy.
	/// </summary>
	[Serializable]
	public abstract class ModelReferenceProperty : ModelProperty
	{
		#region Constructors

		protected internal ModelReferenceProperty(ModelType declaringType, string name, string label, string helptext, string format, bool isStatic, ModelType propertyType, bool isList, bool isReadOnly, bool isPersisted, Attribute[] attributes)
			: base(declaringType, name, label, helptext, format ?? propertyType.Format, isStatic, isList, isReadOnly, isPersisted, attributes)
		{
			this.PropertyType = propertyType;
		}

		#endregion

		#region Properties

		public ModelType PropertyType { get; private set; }

		#endregion

		#region Methods

		/// <summary>
		/// Enumerates over the set of instances represented by the current step.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		public IEnumerable<ModelInstance> GetInstances(ModelInstance instance)
		{
			// Exit immediately if the property is not valid for the specified instance
			if (!DeclaringType.IsInstanceOfType(instance))
				throw new ArgumentException("The current property is not valid for the specified instance.");

			// Return each instance exposed by a list property
			if (IsList)
			{
				ModelInstanceList children = instance.GetList(this);
				if (children != null)
				{
					foreach (ModelInstance child in children)
						yield return child;
				}
			}

			// Return the instance exposed by a reference property
			else
			{
				ModelInstance child = instance.GetReference(this);
				if (child != null)
					yield return child;
			}
		}

		/// <summary>
		/// Gets the formatted value of the property for the specified instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		internal override string GetFormattedValue(ModelInstance instance, string format)
		{
			if (IsList)
				return instance.GetList(this).ToString(format ?? Format);
			else
			{
				var reference = instance.GetReference(this);
				return reference != null ? reference.ToString(format ?? Format) : "";
			}
		}

		#endregion
	}
}
