using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace ExoModel
{
	/// <summary>
	/// Exposes an editable list of instances for a specific list property.
	/// </summary>
	public class ModelInstanceList : ICollection<ModelInstance>, IFormattable
	{
		#region Fields

		ModelInstance owner;
		ModelReferenceProperty property;

		#endregion

		#region Constructors

		internal ModelInstanceList(ModelInstance owner, ModelReferenceProperty property)
		{
			this.owner = owner;
			this.property = property;
		}

		#endregion

		#region Methods

		public ModelInstance this[int index]
		{
			get { return property.PropertyType.GetModelInstance(GetList()[index]); }
			set { GetList()[index] = value.Instance; }
		}
		/// <summary>
		/// Gets the underlying list and coerces it into a valid <see cref="IList"/> implementation.
		/// </summary>
		/// <returns></returns>
		internal IList GetList()
		{
			return property.DeclaringType.ConvertToList(property, property.GetValue(owner == null ? null : owner.Instance));
		}

		/// <summary>
		/// Gets the string representation of the current list, with each item formatted using the
		/// specified format, separated by commas.
		/// </summary>
		/// <param name="format"></param>
		/// <returns></returns>
		public string ToString(string format)
		{
			return ((IFormattable)this).ToString(format, null);
		}

		/// <summary>
		/// Gets the string representation of the current list, with each item formatted using the
		/// default property format, separated by commas.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return ToString(property.Format);
		}

		/// <summary>
		/// Creates and populates a new <see cref="TList"/> with the underlying instances in the current list.
		/// </summary>
		/// <typeparam name="TList"></typeparam>
		/// <returns></returns>
		public object ToList(Type listType)
		{
			return this.ToList(property.PropertyType, listType);
		}

		/// <summary>
		/// Creates and populates a new <see cref="TList"/> with the underlying instances in the current list.
		/// </summary>
		/// <typeparam name="TList"></typeparam>
		/// <returns></returns>
		public TList ToList<TList>()
			where TList : new()
		{
			return (TList)ToList(typeof(TList));
		}

		public IQueryable Where(string predicate, params object[] values)
		{
			throw new NotImplementedException();
			//if (source == null) throw new ArgumentNullException("source");
			//if (predicate == null) throw new ArgumentNullException("predicate");
			//LambdaExpression lambda = ModelExpression.ParseLambda(source.ElementType, typeof(bool), predicate, values);
			//return source.Provider.CreateQuery(
			//    Expression.Call(
			//        typeof(Queryable), "Where",
			//        new CurrentStepPropertyType[] { source.ElementType },
			//        source.Expression, Expression.Quote(lambda)));
		}

		public IQueryable Select(string selector, params object[] values)
		{
			throw new NotImplementedException();
			//if (source == null) throw new ArgumentNullException("source");
			//if (selector == null) throw new ArgumentNullException("selector");
			//LambdaExpression lambda = ModelExpression.ParseLambda(source.ElementType, null, selector, values);
			//return source.Provider.CreateQuery(
			//    Expression.Call(
			//        typeof(Queryable), "Select",
			//        new CurrentStepPropertyType[] { source.ElementType, lambda.Body.CurrentStepPropertyType },
			//        source.Expression, Expression.Quote(lambda)));
		}

		public IQueryable OrderBy(string ordering, params object[] values)
		{
			throw new NotImplementedException();
			//if (source == null) throw new ArgumentNullException("source");
			//if (ordering == null) throw new ArgumentNullException("ordering");
			//ParameterExpression[] parameters = new ParameterExpression[] {
			//    Expression.Parameter(source.ElementType, "") };
			//ModelExpression.ExpressionParser parser = new ModelExpression.ExpressionParser(parameters, ordering, values);
			//IEnumerable<ModelExpression.DynamicOrdering> orderings = parser.ParseOrdering();
			//Expression queryExpr = source.Expression;
			//string methodAsc = "OrderBy";
			//string methodDesc = "OrderByDescending";
			//foreach (ModelExpression.DynamicOrdering o in orderings)
			//{
			//    queryExpr = Expression.Call(
			//        typeof(Queryable), o.Ascending ? methodAsc : methodDesc,
			//        new CurrentStepPropertyType[] { source.ElementType, o.Selector.CurrentStepPropertyType },
			//        queryExpr, Expression.Quote(Expression.Lambda(o.Selector, parameters)));
			//    methodAsc = "ThenBy";
			//    methodDesc = "ThenByDescending";
			//}
			//return source.Provider.CreateQuery(queryExpr);
		}

		public IQueryable GroupBy(string keySelector, string elementSelector, params object[] values)
		{
			throw new NotImplementedException();
			//if (source == null) throw new ArgumentNullException("source");
			//if (keySelector == null) throw new ArgumentNullException("keySelector");
			//if (elementSelector == null) throw new ArgumentNullException("elementSelector");
			//LambdaExpression keyLambda = ModelExpression.ParseLambda(source.ElementType, null, keySelector, values);
			//LambdaExpression elementLambda = ModelExpression.ParseLambda(source.ElementType, null, elementSelector, values);
			//return source.Provider.CreateQuery(
			//    Expression.Call(
			//        typeof(Queryable), "GroupBy",
			//        new CurrentStepPropertyType[] { source.ElementType, keyLambda.Body.CurrentStepPropertyType, elementLambda.Body.CurrentStepPropertyType },
			//        source.Expression, Expression.Quote(keyLambda), Expression.Quote(elementLambda)));
		}

		#endregion

		#region IFormattable

		/// <summary>
		/// Gets the string representation of the current list, with each item formatted using the
		/// specified format, separated by commas.
		/// </summary>
		/// <param name="format"></param>
		/// <param name="formatProvider"></param>
		/// <returns></returns>
		string IFormattable.ToString(string format, IFormatProvider formatProvider)
		{
			return this.Aggregate("", (result, i) => result + (result == "" ? "" : ", ") + i);
		}

		#endregion

		#region ICollection<ModelInstance> Members

		/// <summary>
		/// Adds the specified instance to the list.
		/// </summary>
		/// <param name="item"></param>
		public void Add(ModelInstance item)
		{
			IList list = GetList();
			if (list == null)
			{
				property.DeclaringType.InitializeList(owner, property);
				list = GetList();
			}
			object instance = item.Instance;
			if (!list.Contains(instance))
				list.Add(instance);
		}

		/// <summary>
		/// Removes all of the instances from the list.
		/// </summary>
		public void Clear()
		{
			// Get the list and exit immediately if it does not contain any items
			IList list = GetList();
			if (list == null || list.Count == 0)
				return;

			// Remove all of the items from the list
			ModelEventScope.Perform(() =>
			{
				list.Clear();
			});
		}

		/// <summary>
		/// Determines if the specified instance is in the list.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Contains(ModelInstance item)
		{
			IList list = GetList();
			return list != null && list.Contains(item.Instance);
		}

		/// <summary>
		/// Copies the instances into an array.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		void ICollection<ModelInstance>.CopyTo(ModelInstance[] array, int arrayIndex)
		{
			// Get the list and exit immediately if there are no items to copy
			IList list = GetList();
			if (list == null || list.Count == 0)
				return;

			// Copy all instances in the list to the specified array
			foreach (object instance in list)
				array[arrayIndex++] = property.DeclaringType.GetModelInstance(instance);
		}

		/// <summary>
		/// Gets the number of items in the list.
		/// </summary>
		public int Count
		{
			get
			{
				IList list = GetList();
				return list == null ? 0 : list.Count;
			}
		}

		/// <summary>
		/// Indicates whether the list of read only.
		/// </summary>
		bool ICollection<ModelInstance>.IsReadOnly
		{
			get
			{
				IList list = GetList();
				return list == null || list.IsReadOnly;
			}
		}

		/// <summary>
		/// Removes the specified instance from the list.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Remove(ModelInstance item)
		{
			IList list = GetList();
			if (list == null || !list.Contains(item.Instance))
				return false;

			list.Remove(item.Instance);
			return true;
		}

		/// <summary>
		/// Bulk updates the list to contain the specified set of values.
		/// </summary>
		/// <param name="values">The values the list should contain</param>
		public void Update(IEnumerable<ModelInstance> values)
		{
			property.DeclaringType.UpdateList(owner, property, values);
		}

		#endregion

		#region IEnumerable<ModelInstance> Members

		IEnumerator<ModelInstance> IEnumerable<ModelInstance>.GetEnumerator()
		{
			IList list = GetList();
			if (list != null)
			{
				foreach (object instance in list)
					yield return property.PropertyType.GetModelInstance(instance);
			}
		}

		#endregion

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			IList list = GetList();
			if (list != null)
			{
				foreach (object instance in list)
					yield return property.DeclaringType.GetModelInstance(instance);
			}
		}

		#endregion
	}
}