using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoGraph
{
	/// <summary>
	/// Supports specifying display formats for reference types and reference properties.
	/// </summary>
	/// <remarks>
	/// The <see cref="ReflectionGraphTypeProvider"/> looks for these attributes to determine
	/// the formats for <see cref="GraphType"/> and <see cref="GraphReferenceProperty"/> instances.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
	public class GraphReferenceFormatAttribute : Attribute
	{
		public GraphReferenceFormatAttribute()
		{ }

		public GraphReferenceFormatAttribute(string format)
		{
			this.Format = format;
		}

		public string Format { get; set; }
	}
}
