using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace ExoModel
{
	/// <summary>
	/// Implemented by <see cref="ModelProperty"/> or subclasses based on concrete reflection properties.
	/// </summary>
	public interface IReflectionModelProperty
	{
		PropertyInfo PropertyInfo { get; }
	}
}