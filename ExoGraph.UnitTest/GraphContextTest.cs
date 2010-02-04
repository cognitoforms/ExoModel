using ExoGraph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExoGraph.UnitTest
{
	/// <summary>
	///This is a test class for GraphContextTest and is intended
	///to contain all GraphContextTest Unit Tests
	///</summary>
	[TestClass]
	public abstract class GraphContextTest<TUser, TCategory, TPriority, TRequest, TRequestList, TCategoryList>
		where TUser : IUser<TUser, TCategory, TPriority, TRequest, TRequestList, TCategoryList>, new()
		where TCategory : ICategory<TUser, TCategory, TPriority, TRequest, TRequestList, TCategoryList>, new()
		where TPriority : IPriority<TUser, TCategory, TPriority, TRequest, TRequestList, TCategoryList>, new()
		where TRequest : IRequest<TUser, TCategory, TPriority, TRequest, TRequestList, TCategoryList>, new()
		where TRequestList : ICollection<TRequest>
		where TCategoryList : ICollection<TCategory>
	{
		/// <summary>
		/// Verifies that an <see cref="IGraphContextProvider"/> has been assigned.
		///</summary>
		[TestMethod]
		public virtual void ProviderTest()
		{
			// Verify that a provider has been set
			Assert.IsNotNull(GraphContext.Provider);
		}

		/// <summary>
		/// Verifies that a current <see cref="GraphContext"/> exists.
		///</summary>
		[TestMethod()]
		public virtual void CurrentTest()
		{
			// Verify that the current context is available
			Assert.IsNotNull(GraphContext.Current);
		}

		/// <summary>
		/// Verifies that base types have been correctly assigned for the test model.
		///</summary>
		[TestMethod()]
		public virtual void SetBaseTypeTest()
		{
			Assert.Inconclusive("SetBaseType() not tested.");
		}

		/// <summary>
		/// Verify that the graph is saved when <see cref="GraphContext.Save"/> is called.
		///</summary>
		[TestMethod()]
		public virtual void SaveTest()
		{

		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.OnStartTrackingList"/> is called when
		/// a list property is first assessed or is assigned a new list value.
		///</summary>
		[TestMethod()]
		public virtual void OnStartTrackingListTest()
		{
			// Verify list tracking on list on first access

			// Verify list tracking on new list assigned to property
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.OnStopTrackingList"/> is called when
		/// a list property is assigned a new value and the original list was being tracked.
		///</summary>
		[TestMethod()]
		public virtual void OnStopTrackingListTest()
		{
			// Verify list tracking has stopped on lists removed from the graph
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.OnSave"/> is called when the graph is saved.
		///</summary>
		[TestMethod()]
		public virtual void OnSaveTest()
		{

		}

		/// <summary>
		/// Verify that <see cref="GraphContext.OnPropertyGet"/> is called when a property is accessed.
		///</summary>
		[TestMethod()]
		public virtual void OnPropertyGetTest()
		{
			// Test a value property

			// Test an instance reference property

			// Test a list reference property
		}

		/// <summary>
		/// Verify that <see cref="GraphContext.OnPropertyChanged"/> is called when a property value is changed.
		///</summary>
		[TestMethod()]
		public virtual void OnPropertyChangedTest()
		{
			// Get the relevant graph types and create real instances
			GraphType userType = GraphContext.Current.GetGraphType<TUser>();
			GraphType requestType = GraphContext.Current.GetGraphType<TRequest>();
			TRequest request = new TRequest();
			TUser user = new TUser();

			// Test a value property
			bool valueChanged = false;
			requestType.ValueChange += (sender, args) => 
				valueChanged = args.Property.Name == "Description" && 
				String.IsNullOrEmpty((string)args.OldValue) && 
				args.NewValue == "My New Description";
			request.Description = "My New Description";
			Assert.IsTrue(valueChanged, "Property change was not correctly raised on value property.");

			// Test a reference property
			bool referenceChanged = false;
			requestType.ReferenceChange += (sender, args) =>
				referenceChanged = args.Property.Name == "AssignedTo" &&
				args.OldValue == null &&
				args.NewValue.Instance == (object)user;
			request.AssignedTo = user;
			Assert.IsTrue(referenceChanged, "Property change was not correctly raised on reference property.");
		}

		/// <summary>
		/// Verify that <see cref="GraphContext.OnListChanged"/> is called with items are added or removed from a list.
		///</summary>
		[TestMethod()]
		public virtual void OnListChangedTest()
		{
			// Get the relevant graph types and create real instances
			GraphType userType = GraphContext.Current.GetGraphType<TUser>();
			GraphType requestType = GraphContext.Current.GetGraphType<TRequest>();
			TRequest request = new TRequest();
			TUser user = new TUser();

			// Test adding to a list
			bool listChanged = false;
			userType.ListChange += (sender, args) =>
				listChanged = args.Property.Name == "Requests" &&
				args.Added.Any() && args.Added.FirstOrDefault().Instance == (object)request &&
				!args.Removed.Any();
			user.Requests.Add(request);
			Assert.IsTrue(listChanged, "Property change was not correctly raised on list property.");

			// Test removing from a list
			listChanged = false;
			userType.ListChange += (sender, args) =>
				listChanged = args.Property.Name == "Requests" &&
				args.Removed.Any() && args.Removed.FirstOrDefault().Instance == (object)request &&
				!args.Added.Any();
			user.Requests.Remove(request);
			Assert.IsTrue(listChanged, "Property change was not correctly raised on list property.");
	
			// Test bulk adds

			// Test clears
		}

		/// <summary>
		/// Verify that <see cref="GraphContext.OnInit"/> is called when a new instance is created.
		///</summary>
		[TestMethod()]
		public virtual void OnInitTest()
		{

		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.GetInstance"/> returns a value new or existing instance.
		///</summary>
		[TestMethod()]
		public virtual void GetInstanceTest()
		{
			// Test creating new instance

			// Test creating existing instance

			// Test getting cached reference to existing instance
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.GetId"/> returns a valid string identifier for a graph instance.
		///</summary>
		[TestMethod()]
		public virtual void GetIdTest()
		{

		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.GetGraphType"/> correctly returns the requested <see cref="GraphType"/>.
		///</summary>
		[TestMethod()]
		public virtual void GetGraphTypeTest()
		{
			VerifyType(GraphContext.Current.GetGraphType("User"));
			VerifyType(GraphContext.Current.GetGraphType("Request"));
			VerifyType(GraphContext.Current.GetGraphType("Category"));
			VerifyType(GraphContext.Current.GetGraphType("Priority"));
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.GetGraphInstance"/> returns the <see cref="GraphInstance"/>
		/// associated with the specified real graph object.
		///</summary>
		[TestMethod()]
		public virtual void GetGraphInstanceTest()
		{

		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.DeleteInstance"/> successfully marks the
		/// specified instance for deletion.
		///</summary>
		[TestMethod()]
		public virtual void DeleteInstanceTest()
		{

		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.CreateGraphType"/> returns a new <see cref="GraphType"/>
		/// that corresponds to the specified type name.
		///</summary>
		[TestMethod()]
		public virtual void CreateGraphTypeTest()
		{

		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.ConvertToList"/> returns a valid <see cref="IList"/> instance
		/// given the underlying value of a list property.
		///</summary>
		[TestMethod()]
		public virtual void ConvertToListTest()
		{

		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.BeginTransaction"/> creates a valid <see cref="GraphTransaction"/>
		/// and that the transaction successfully commits and rolls back when requested.
		///</summary>
		[TestMethod()]
		public virtual void BeginTransactionTest()
		{

		}

		/// <summary>
		/// Verifies that the specified <see cref="GraphType"/> is not null and has the correct meta data.
		/// </summary>
		/// <param name="type"></param>
		void VerifyType(GraphType type)
		{
			Assert.IsNotNull(type, "The graph type was not found");
			switch (type.Name)
			{
				case "Request":
					Assert.AreEqual<int>(6, type.Properties.Count, "The Request type does not have the correct number of properties.");
					Assert.IsTrue(type.Properties["User"] is GraphReferenceProperty, "Request graph type does not have expected User value property.");
					Assert.IsTrue(type.Properties["Description"] is GraphValueProperty, "Request graph type does not have expected Description value property.");
					break;
			}
		}
	}
}
