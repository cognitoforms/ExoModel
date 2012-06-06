using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	/// <summary>
	/// Implemented by <see cref="ModelType"/> or subclasses based on concrete reflection types.
	/// </summary>
	public interface IReflectionModelType
	{
		Type UnderlyingType { get; }
	}
}
