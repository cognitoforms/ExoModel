using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	public interface IModelTypeProvider
	{
		/// <summary>
		/// Gets the unique name of the <see cref="ModelType"/> for the specified model object instance.
		/// </summary>
		/// <param name="instance">The actual model object instance</param>
		/// <returns>The unique name of the model type for the instance if it is a valid model type, otherwise null</returns>
		string GetModelTypeName(object instance);

		/// <summary>
		/// Gets the unique name of the <see cref="ModelType"/> that corresponds to the specified <see cref="Type"/>.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		string GetModelTypeName(Type type);

		/// <summary>
		/// Creates a <see cref="ModelType"/> that corresponds to the specified type name.
		/// </summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		ModelType CreateModelType(string typeName);
	}
}
