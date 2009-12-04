using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;

namespace ExoGraph
{
	/// <summary>
	/// Base class for graph contexts that work with strongly-typed object graphs based on compiled types
	/// using inheritence and declared properties for associations and intrinsic types.
	/// </summary>
	public abstract class StronglyTypedGraphContext : GraphContext
	{
		#region Fields

		IDictionary<Type, GraphType> graphTypes;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new <see cref="StronglyTypesGraphContext"/> based on the specified types
		/// and also including properties declared on the specified base types.
		/// </summary>
		/// <param name="types">The types to create graph types from</param>
		/// <param name="baseTypes">The base types that contain properties to include on graph types</param>
		public StronglyTypedGraphContext(IEnumerable<Type> types, IEnumerable<Type> baseTypes)
		{
			// The list of types cannot be null
			if (types == null)
				throw new ArgumentNullException("types");

			// The list of base types is not required, so convert null to empty set
			if (baseTypes == null)
				baseTypes = new Type[0];

			// Infer the graph types based on the specified types
			graphTypes = InferGraphTypes(types, baseTypes);
		}
		
		#endregion

		#region Methods

		/// <summary>
		/// Gets the <see cref="GraphType"/> for the specified graph object instance.
		/// </summary>
		/// <param name="instance">The actual graph object instance</param>
		/// <returns>The graph type of the object if it is a valid graph type, otherwise null</returns>
		protected internal override GraphType GetGraphType(object instance)
		{
			if (instance == null)
				return null;

			GraphType graphType;
			graphTypes.TryGetValue(instance.GetType(), out graphType);
			return graphType;
		}

		/// <summary>
		/// Infers the <see cref="GraphType"/> instances based on the specified types and also
		/// includes properties declared on the specified base types.
		/// </summary>
		/// <param name="types">The types to create graph types from</param>
		/// <param name="baseTypes">The base types that contain properties to include on graph types</param>
		/// <returns>A dictionary of inferred <see cref="Type"/> and <see cref="GraphType"/> pairs</returns>
		IDictionary<Type, GraphType> InferGraphTypes(IEnumerable<Type> types, IEnumerable<Type> baseTypes)
		{
			// Create instance types for each specified type
			SortedDictionary<Type, GraphType> graphTypes = new SortedDictionary<Type, GraphType>(new TypeComparer());
			foreach (Type type in types)
			{

				GraphType graphType = CreateGraphType(type.Name, type.AssemblyQualifiedName, type.GetCustomAttributes(true).Cast<Attribute>().ToArray());
				graphTypes.Add(type, graphType);
			}

			// Create a dictionary of valid declaring types to introspect
			Dictionary<Type, Type> declaringTypes = new Dictionary<Type, Type>();
			foreach (Type type in types)
			{
				if (!declaringTypes.ContainsKey(type))
					declaringTypes.Add(type, type);
			}
			foreach (Type baseType in baseTypes)
			{
				if (!declaringTypes.ContainsKey(baseType))
					declaringTypes.Add(baseType, baseType);
			}

			// Declare graph type variables to track base and sub types
			GraphType baseGraphType = null;
			GraphType subGraphType = null;

			// Initialize each instance type
			foreach (KeyValuePair<Type, GraphType> typePair in graphTypes)
			{
				// Establish parent-child relationships between instance types
				Type type = typePair.Key;
				GraphType graphType = typePair.Value;
				Type baseType = type.BaseType;
				while (baseType != null && !graphTypes.TryGetValue(baseType, out baseGraphType))
					baseType = baseType.BaseType;
				if (baseGraphType != null)
					SetBaseType(graphType, baseGraphType);

				// Determine if any inherited properties on the type have been replaced using the new keyword
				Dictionary<string, bool> isNewProperty = new Dictionary<string,bool>();
				foreach (PropertyInfo property in type.GetProperties())
					isNewProperty[property.Name] = isNewProperty.ContainsKey(property.Name);

				// Process all properties on the instance type to create references
				foreach (PropertyInfo property in type.GetProperties())
				{
					// Exit immediately if the property was not in the list of valid declaring types
					if (!declaringTypes.ContainsKey(property.DeclaringType) || (isNewProperty[property.Name] && property.DeclaringType != type))
						continue;

					// Copy properties inherited from base graph types
					if (baseGraphType != null && baseGraphType.Properties.Contains(property.Name) && !isNewProperty[property.Name]) 
						AddProperty(graphType, baseGraphType.Properties[property.Name]);

					// Create references based on properties that relate to other instance types
					else if (graphTypes.TryGetValue(property.PropertyType, out subGraphType))
						AddProperty(graphType, property.Name, property.GetGetMethod().IsStatic, property.GetGetMethod().IsStatic, subGraphType, false, property.GetCustomAttributes(true).Cast<Attribute>().ToArray());

					// Create references based on properties that are lists of other instance types
					else if (typeof(IList).IsAssignableFrom(property.PropertyType) &&
						property.PropertyType.GetProperty("Item", new Type[] { typeof(int) }) != null &&
						graphTypes.TryGetValue(property.PropertyType.GetProperty("Item", new Type[] { typeof(int) }).PropertyType, out subGraphType))
						AddProperty(graphType, property.Name, property.GetGetMethod().IsStatic, property.GetGetMethod().IsStatic, subGraphType, true, property.GetCustomAttributes(true).Cast<Attribute>().ToArray());

					// Create values for all other properties
					else
						AddProperty(graphType, property.Name, property.GetGetMethod().IsStatic, property.PropertyType, property.GetCustomAttributes(true).Cast<Attribute>().ToArray());
				}
			}

			// Return the inferred graph types
			return graphTypes;
		}

		protected internal override object GetProperty(object instance, string property)
		{
			return instance.GetType().GetProperty(property).GetValue(instance, null);
		}

		protected internal override void SetProperty(object instance, string property, object value)
		{
			instance.GetType().GetProperty(property).SetValue(instance, value, null);
		}

		protected internal override object GetProperty(GraphType type, string property)
		{
			return Type.GetType(type.QualifiedName).GetProperty(property).GetValue(null, null);
		}

		protected internal override void SetProperty(GraphType type, string property, object value)
		{
			Type.GetType(type.QualifiedName).GetProperty(property).SetValue(null, value, null);
		}

		#endregion

		#region TypeComparer

		/// <summary>
		/// Specialized <see cref="IComparer<T>"/> implementation that sorts types
		/// first in order of inheritance and second by name.
		/// </summary>
		class TypeComparer : Comparer<Type>
		{
			public override int Compare(Type x, Type y)
			{
				if (x == y)
					return 0;
				else if (x.IsSubclassOf(y))
					return 1;
				else if (y.IsSubclassOf(x))
					return -1;
				else if (x.BaseType == y.BaseType)
					return x.FullName.CompareTo(y.FullName);
				else
					return GetQualifiedTypeName(x).CompareTo(GetQualifiedTypeName(y));
			}

			/// <summary>
			/// Gets the fully-qualified name of the type including all base classes.
			/// </summary>
			/// <param name="type"></param>
			/// <returns></returns>
			string GetQualifiedTypeName(Type type)
			{
				string typeName = "";
				while (type != null)
				{
					typeName = type.FullName + ":" + typeName;
					type = type.BaseType;
				}
				return typeName;
			}
		}

		#endregion
	}
}
