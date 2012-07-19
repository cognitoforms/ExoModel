using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.ETL
{
	/// <summary>
	/// This class is used to hold a relationship between
	/// an expression and a property path.  It is strictly utilized
	/// by the Mapping classes.
	/// </summary>
	public class ExpressionToProperty : IComparable<ExpressionToProperty>, IEquatable<ExpressionToProperty>
	{
		public string Expression { get; set; }
		public string PropertyPath { get; set; }

		public int CompareTo(ExpressionToProperty obj)
		{
			return (this.PropertyPath + this.Expression).CompareTo(obj.PropertyPath + obj.Expression);
		}

		public bool Equals(ExpressionToProperty other)
		{
			return (this.PropertyPath + this.Expression).Equals(other.PropertyPath + other.Expression);
		}
	}

	/// <summary>
	/// All mapping sub classes must populate the Mapping dictionary.
	/// </summary>
	public abstract class AbstractMapping : IMapping
	{
		protected IEnumerable<ExpressionToProperty> Mapping;   //This holds a mapping between the absolute type property name, and the expression to use to convert.

		public IEnumerable<ExpressionToProperty> GetMapping()
		{
			return Mapping;
		}

		public ExpressionToProperty GetIdMappingElement()
		{
			foreach (ExpressionToProperty item in Mapping)
			{
				if (item.PropertyPath == "Id")
				{
					return item;
				}
			}
			return null;
		}
	}
}
