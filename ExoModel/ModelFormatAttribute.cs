using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	/// <summary>
	/// Supports specifying display formats for reference types and reference properties.
	/// </summary>
	/// <remarks>
	/// The <see cref="ReflectionModelTypeProvider"/> looks for these attributes to determine
	/// the formats for <see cref="ModelType"/> and <see cref="ModelReferenceProperty"/> instances.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
	public class ModelFormatAttribute : Attribute
	{
		public ModelFormatAttribute()
		{ }

		public ModelFormatAttribute(string format)
		{
			this.Format = format;
		}

		public string Format { get; set; }
	}
}
