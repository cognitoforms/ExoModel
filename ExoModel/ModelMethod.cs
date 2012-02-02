using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExoModel
{
	public abstract class ModelMethod
	{
		#region Constructors

		protected ModelMethod(ModelType declaringType, string name, bool isStatic, Attribute[] attributes)
		{
			// Ensure the method name is unique
			int index = 0;
			while (declaringType.Methods.Contains(name + (index == 0 ? "" : index.ToString())))
				index++;

			this.DeclaringType = declaringType;
			this.Name = name + (index == 0 ? "" : index.ToString());
			this.IsStatic = isStatic;
			this.Parameters = new ModelMethodParameterList();
		}

		#endregion

		#region Properties

		public ModelType DeclaringType { get; private set; }

		public string Name { get; private set; }

		public bool IsStatic { get; private set; }

		public Attribute[] Attributes { get; private set; }

		public ModelMethodParameterList Parameters { get; private set;  }

		#endregion

		#region Methods

		/// <summary>
		/// Invokes the method on the specified model instance.
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		public abstract object Invoke(ModelInstance instance, params object[] args);

		/// <summary>
		/// Adds a <see cref="ModelMethodParameter"/> to the current method.
		/// </summary>
		/// <param name="parameter"></param>
		protected void AddParameter(ModelMethodParameter parameter)
		{
			// Set the parameter index
			parameter.Index = Parameters.Count;

			// Add the parameter
			Parameters.Add(parameter);
		}

		/// <summary>
		/// Indicates whether the current method has one or more attributes of the specified type.
		/// </summary>
		/// <typeparam name="TAttribute"></typeparam>
		/// <returns></returns>
		public bool HasAttribute<TAttribute>()
			where TAttribute : Attribute
		{
			return GetAttributes<TAttribute>().Length > 0;
		}

		/// <summary>
		/// Returns an array of attributes defined on the current method.
		/// </summary>
		/// <typeparam name="TAttribute"></typeparam>
		/// <returns></returns>
		public TAttribute[] GetAttributes<TAttribute>()
			where TAttribute : Attribute
		{
			List<TAttribute> matches = new List<TAttribute>();

			// Find matching attributes on the current type
			foreach (Attribute attribute in Attributes)
			{
				if (attribute is TAttribute)
					matches.Add((TAttribute)attribute);
			}

			return matches.ToArray();
		}

		#endregion
	}
}
