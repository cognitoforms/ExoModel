using System;
using System.Collections;
using System.Reflection;
using PostSharp.Laos;
using PostSharp.Extensibility;

namespace ExoGraph.Injection
{
	#region ExoGraphAttribute

	/// <summary>
	/// 
	/// </summary>
	[MulticastAttributeUsage(MulticastTargets.Class | MulticastTargets.Struct)]
	[Serializable]
	public class ExoGraphAttribute : CompoundAspect
	{
		/// <summary>
		/// Method called at compile time to get individual aspects required by the current compound aspect.
		/// </summary>
		/// <param name="targetElement">Metadata element (<see cref="Type"/> in this case) to which
		/// the current custom attribute instance is applied.</param>
		/// <param name="collection">Collection of aspects to which individual aspects should be added.</param>
		public override void ProvideAspects(object targetElement, LaosReflectionAspectCollection collection)
		{
			// Get the target type
			Type type = (Type)targetElement;

			// Add an aspect to track the graph instance for each real instance
			collection.AddAspect(type, new InjectionGraphContext.InstanceAspect());

			// Add aspects to track interactions with all public properties
			foreach (PropertyInfo property in type.UnderlyingSystemType.GetProperties())
			{
				// Only consider properties declared on this type that can be read, are not static, and either can be set or are mutable lists
				if (property.DeclaringType == type && property.CanRead && !property.GetGetMethod().IsStatic && (property.CanWrite || typeof(IList).IsAssignableFrom(property.PropertyType)))
				{
					collection.AddAspect(property.GetGetMethod(), new InjectionGraphContext.OnPropertyGetAspect(property));

					// Only add aspects to setters for writable properties
					if (property.CanWrite)
						collection.AddAspect(property.GetSetMethod(), new InjectionGraphContext.OnPropertySetAspect(property));
				}
			}
		}
	}

	#endregion

}
