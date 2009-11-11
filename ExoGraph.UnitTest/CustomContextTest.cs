using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.ObjectModel;
using System.Collections;

namespace ExoGraph.UnitTest
{
	/// <summary>
	/// Unit test that demonstrates a custom <see cref="GraphContext"/> subclass implementation and
	/// leverages the <see cref="ContextTestBase"/> base class to ensure it works correctly.
	/// </summary>
	[TestClass]
	public class CustomContextTest : ContextTestBase
	{
		public CustomContextTest()
		{
			new GraphContextProvider().CreateContext += (sender, e) =>
			{
				e.Context = Entity.Context;
			};
		}

	}

	#region Entity

	/// <summary>
	/// Example of a base class for entities within an object graph that 
	/// integrate with the <see cref="GraphContext"/> to expose graph events.
	/// </summary>
	public class Entity
	{
		#region GraphContext

		internal class GraphContext : StronglyTypedGraphContext
		{
			public GraphContext()
				: base(
					new Type[] { typeof(Customer), typeof(Contact) },
					new Type[] { typeof(CustomerBase) })
			{ }

			protected override GraphInstance GetInstance(object instance)
			{
				if (instance is Entity)
					return ((Entity)instance).instance;
				return null;
			}

			public override object CreateInstance(GraphType type, string id)
			{
				if (id == null)
					return Type.GetType(type.Name).GetConstructor(Type.EmptyTypes).Invoke(null);

				throw new NotSupportedException("Creating instances of existing objects is not supported by this test context.");
			}

			protected override void DeleteInstance(object instance)
			{

			}

			internal new GraphInstance GetInstance(Entity entity)
			{
				return entity.instance;
			}

			/// <summary>
			/// Redeclared to allow invocation by <see cref="Entity"/> without exposing implementation publicly.
			/// </summary>
			/// <param name="instance"></param>
			/// <returns></returns>
			internal new GraphInstance OnInit(object instance)
			{
				return base.OnInit(instance);
			}

			/// <summary>
			/// Redeclared to allow invocation by <see cref="Entity"/> without exposing implementation publicly.
			/// </summary>
			/// <param name="instance"></param>
			/// <param name="property"></param>
			internal new void OnPropertyGet(GraphInstance instance, string property)
			{
				base.OnPropertyGet(instance, property);
			}

			/// <summary>
			/// Redeclared to allow invocation by <see cref="Entity"/> without exposing implementation publicly.
			/// </summary>
			/// <param name="instance"></param>
			/// <param name="property"></param>
			/// <param name="oldValue"></param>
			/// <param name="newValue"></param>
			internal new void OnPropertyChanged(GraphInstance instance, string property, object oldValue, object newValue)
			{
				base.OnPropertyChanged(instance, property, oldValue, newValue);
			}

			/// <summary>
			/// Redeclared to allow invocation by <see cref="Entity"/> without exposing implementation publicly.
			/// </summary>
			/// <param name="instance"></param>
			/// <param name="property"></param>
			/// <param name="added"></param>
			/// <param name="removed"></param>
			internal new void OnListChanged(GraphInstance instance, string property, IEnumerable added, IEnumerable removed)
			{
				base.OnListChanged(instance, property, added, removed);
			}
		}

		#endregion

		static readonly GraphContext context = new GraphContext();

		GraphInstance instance;
		Dictionary<string, object> properties = new Dictionary<string, object>();

		public Entity()
		{
			instance = context.OnInit(this);
		}

		public static ExoGraph.GraphContext Context
		{
			get
			{
				return (ExoGraph.GraphContext)context;
			}
		}

		protected TValue GetProperty<TValue>(string name)
		{
			// Notify the graph instance that the property value is being retrieved
			context.OnPropertyGet(instance, name);

			TValue value = default(TValue);
			object v;
			if (properties.TryGetValue(name, out v))
				value = (TValue)v;
			return value;
		}

		protected void SetProperty<TValue>(string name, TValue value)
		{
			TValue oldValue = GetProperty<TValue>(name);
			if ((oldValue == null && value == null) || (oldValue != null && oldValue.Equals(value)))
				return;

			properties[name] = value;

			// Notify the graph instance that the property value has changed
			context.OnPropertyChanged(instance, name, oldValue, value);
		}

		/// <summary>
		/// Allows subclasses to raise custom events.
		/// </summary>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="customEvent"></param>
		protected void RaiseEvent<TEvent>(TEvent customEvent)
		{
			instance.RaiseEvent<TEvent>(customEvent);
		}
	}

	#endregion

	#region EntityList

	public class EntityList<TEntity> : Collection<TEntity>
		where TEntity : Entity
	{
		Entity owner;
		string property;

		internal EntityList(Entity owner, string property)
		{
			this.owner = owner;
			this.property = property;
		}

		protected override void ClearItems()
		{
			TEntity[] removed = new TEntity[Count];
			CopyTo(removed, 0);
			base.ClearItems();
			OnChanged((IEnumerable)null, removed);
		}

		protected override void InsertItem(int index, TEntity item)
		{
			base.InsertItem(index, item);
			OnChanged(new Entity[] { item }, (IEnumerable)null);
		}

		protected override void RemoveItem(int index)
		{
			Entity item = this[index];
			base.RemoveItem(index);
			OnChanged((IEnumerable)null, new Entity[] { item });
		}

		protected override void SetItem(int index, TEntity item)
		{
			Entity oldItem = this[index];
			base.SetItem(index, item);
			OnChanged(new Entity[] { item }, new Entity[] { oldItem });
		}

		void OnChanged(IEnumerable added, IEnumerable removed)
		{
			((Entity.GraphContext)Entity.Context).OnListChanged(((Entity.GraphContext)Entity.Context).GetInstance(owner), property, added, removed);
		}
	}

	#endregion

	#region CustomerBase

	public abstract class CustomerBase : Entity
	{
		public string Name
		{
			get { return GetProperty<string>("Name"); }
			set { SetProperty<string>("Name", value); }
		}
	}

	#endregion

	#region Customer

	public class Customer : CustomerBase
	{
		EntityList<Contact> otherContacts;
		
		public Customer()
		{
			otherContacts = new EntityList<Contact>(this, "OtherContacts");
		}

		public Contact PrimaryContact
		{
			get { return GetProperty<Contact>("PrimaryContact"); }
			set { SetProperty<Contact>("PrimaryContact", value); }
		}

		public int YearFounded
		{
			get { return GetProperty<int>("YearFounded"); }
			set { SetProperty<int>("YearFounded", value); }
		}

		public EntityList<Contact> OtherContacts
		{
			get
			{
				return otherContacts;
			}
		}

		public void UpdateStockData()
		{
			RaiseEvent<UpdateStockDataEvent>(new UpdateStockDataEvent());
		}

		public class UpdateStockDataEvent
		{
		}
	}

	#endregion

	#region Contact

	public class Contact : Entity
	{
		public string Name
		{
			get { return GetProperty<string>("Name"); }
			set { SetProperty<string>("Name", value); }
		}

		public string Phone
		{
			get { return GetProperty<string>("Phone"); }
			set { SetProperty<string>("Phone", value); }
		}

		public string Email
		{
			get { return GetProperty<string>("Email"); }
			set { SetProperty<string>("Email", value); }
		}

	}

	#endregion
}
