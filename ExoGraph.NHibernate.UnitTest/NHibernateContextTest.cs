using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NHibernate;
using System.Collections;
using System.Collections.ObjectModel;
using ExoGraph.UnitTest;
using ExoGraph.NHibernate.Collection;

namespace ExoGraph.NHibernate.UnitTest
{
	/// <summary>
	/// Summary description for UnitTest1
	/// </summary>
	[TestClass]
	public class NHibernateContextTest : NHibernateFixtureBase
	{
		public NHibernateContextTest()
		{
			new GraphContextProvider().CreateContext += (sender, e) =>
				{
					e.Context = new NHibernateGraphContext(new Type[] { typeof(Request), typeof(Priority), typeof(User), typeof(Category) });
				};
		}

		/// <summary>
		/// Verifies that an <see cref="IGraphContextProvider"/> has been assigned.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod]
		public override void ProviderTest()
		{
			base.ProviderTest();
		}

		/// <summary>
		/// Verifies that a current <see cref="GraphContext"/> exists.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void CurrentTest()
		{
			base.CurrentTest();
		}

		/// <summary>
		/// Verifies that base types have been correctly assigned for the test model.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void SetBaseTypeTest()
		{
			base.SetBaseTypeTest();
		}

		/// <summary>
		/// Verify that the graph is saved when <see cref="GraphContext.Save"/> is called.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void SaveTest()
		{
			base.SaveTest();
		}

		/// <summary>
		/// Verify that <see cref="GraphContext.OnPropertyGet"/> is called when a property is accessed.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void OnPropertyGetTest()
		{
			base.OnPropertyGetTest();
		}

		/// <summary>
		/// Verify that <see cref="GraphContext.OnPropertyChanged"/> is called when a property value is changed.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void OnPropertyChangedTest()
		{
			base.OnPropertyChangedTest();
		}

		/// <summary>
		/// Verify that <see cref="GraphContext.OnListChanged"/> is called with items are added or removed from a list.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void OnListChangedTest()
		{
			base.OnListChangedTest();
		}

		/// <summary>
		/// Verify that <see cref="GraphContext.OnInit"/> is called when a new instance is created.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void OnInitTest()
		{
			base.OnInitTest();
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.GetInstance"/> returns a value new or existing instance.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void GetInstanceTest()
		{
			base.GetInstanceTest();
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.GetId"/> returns a valid string identifier for a graph instance.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void GetIdTest()
		{
			base.GetIdTest();
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.GetGraphType"/> correctly returns the requested <see cref="GraphType"/>.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void GetGraphTypeTest()
		{
			base.GetGraphTypeTest();
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.GetGraphInstance"/> returns the <see cref="GraphInstance"/>
		/// associated with the specified real graph object.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void GetGraphInstanceTest()
		{
			base.GetGraphInstanceTest();
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.DeleteInstance"/> successfully marks the
		/// specified instance for deletion.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void DeleteInstanceTest()
		{
			base.DeleteInstanceTest();
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.CreateGraphType"/> returns a new <see cref="GraphType"/>
		/// that corresponds to the specified type name.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void CreateGraphTypeTest()
		{
			base.CreateGraphTypeTest();
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.ConvertToList"/> returns a valid <see cref="IList"/> instance
		/// given the underlying value of a list property.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void ConvertToListTest()
		{
			base.ConvertToListTest();
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.BeginTransaction"/> creates a valid <see cref="GraphTransaction"/>
		/// and that the transaction successfully commits and rolls back when requested.
		///</summary>
		[DeploymentItem("hibernate.cfg.xml"), TestMethod()]
		public override void BeginTransactionTest()
		{
			base.BeginTransactionTest();
		}

		public override User CreateNewUser()
		{
			return DataBindingFactory.Create<User>();
		}

		public override Category CreateNewCategory()
		{
			return DataBindingFactory.Create<Category>();
		}

		public override Priority CreateNewPriority()
		{
			return DataBindingFactory.Create<Priority>();
		}

		public override Request CreateNewRequest()
		{
			return DataBindingFactory.Create<Request>();
		}
	}

	public class Priority : IPriority<User, Category, Priority, Request, IList<Request>, IList<Category>>
	{
		public virtual int PriorityId { get; set; }
		public virtual string Name { get; set; }

		public Priority()
		{
		}
	}

	public class User : IUser<User, Category, Priority, Request, IList<Request>, IList<Category>>
	{
		public virtual Guid UserId { get; set; }
		public virtual string UserName { get; set; }
		public virtual IList<Request> Requests { get; set; }
		public virtual IList<Request> Assignments { get; set; }

		public User()
		{
			Requests = new FullyObservableCollection<Request>();
			Assignments = new FullyObservableCollection<Request>();
		}
	}

	public class Category : ICategory<User, Category, Priority, Request, IList<Request>, IList<Category>>
	{
		private IList<Category> childCategories;

		public virtual int CategoryId { get; set; }
		public virtual string Name { get; set; }
		public virtual Category ParentCategory { get; set; }
		public virtual IList<Category> ChildCategories
		{
			get
			{
				return childCategories;
			}
		}

		public Category()
		{
			childCategories = new FullyObservableCollection<Category>();
		}
	}

	public class Request : IRequest<User, Category, Priority, Request, IList<Request>, IList<Category>>
	{
		public virtual int RequestId { get; set; }
		public virtual User User { get; set; }
		public virtual Category Category { get; set; }
		public virtual Priority Priority { get; set; }
		public virtual string Description { get; set; }
		public virtual User AssignedTo { get; set; }

		public Request()
		{
		}
	}
}
