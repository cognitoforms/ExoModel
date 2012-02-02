using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;

namespace ExoModel
{
	/// <summary>
	/// Exposes a list of <see cref="ModelStep"/> instances keyed by name.
	/// </summary>
	public class ModelStepList : ReadOnlyList<ModelStep>
	{
		public ModelStepList()
			: base()
		{
		}

		internal ModelStepList(IEnumerable<ModelStep> modelSteps)
			: base(modelSteps)
		{
		}

		protected override string GetName(ModelStep item)
		{
			return item.Property.Name + "<" + (item.Filter != null ? item.Filter.Name : item.Property.DeclaringType.Name) + ">";
		}
	}
}
