﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoGraph;
using System.Linq.Expressions;
using System.Collections.ObjectModel;

namespace ExoGraph.UnitTest
{
	public class TestEntity : IGraphInstance
	{
		GraphInstance instance;
		Dictionary<string, object> values = new Dictionary<string, object>();

		protected TestEntity()
		{
			instance = new GraphInstance(this);
		}

		~TestEntity()
		{
			if (Finalized != null)
			{
				Finalized(this, EventArgs.Empty);
				Finalized = null;
			}
		}

		public event EventHandler Finalized;

		internal int? Id { get; set; }

		protected TValue Get<TValue>(Expression<Func<TValue>> property)
		{
			// Get the name of the property being fetched
			string propertyName = ((MemberExpression)property.Body).Member.Name;
			
			// Raise the property get notification
			instance.OnPropertyGet(propertyName);
			
			// Return a value if assigned
			object value;
			if (values.TryGetValue(propertyName, out value))
				return (TValue)value;

			// Automatically initialize list fields if null when accessed
			if (typeof(TValue).IsGenericType && typeof(TValue).GetGenericTypeDefinition() == typeof(ICollection<>))
			{
				value = Activator.CreateInstance(typeof(ObservableCollection<>).MakeGenericType(typeof(TValue).GetGenericArguments()[0]));
				values[propertyName] = value;
			}

			// Otherwise return null/default 
			return default(TValue);
		}

		protected void Set<TValue>(Expression<Func<TValue>> property, TValue value)
		{
			string propertyName = ((MemberExpression)property.Body).Member.Name;
			TValue oldValue = Get(property);
			values[propertyName] = value;
			instance.OnPropertySet(propertyName, oldValue, value);
		}

		GraphInstance IGraphInstance.Instance
		{
			get { return instance; }
		}
	}
}
