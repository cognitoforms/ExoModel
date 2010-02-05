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

		}

		/// <summary>
		/// Verify that the graph is saved when <see cref="GraphContext.Save"/> is called.
		///</summary>
		[TestMethod()]
		public virtual void SaveTest()
		{
			GraphInstance userInstance;
			GraphSaveEvent saveEvent = null;

			// Start a transaction to track identity changes that occur 
			using (var transaction = GraphContext.Current.BeginTransaction())
			{
				// Create a new user
				TUser user = new TUser();

				// Get the graph instance for the new user
				userInstance = GraphContext.Current.GetGraphInstance(user);

				// Set the username for the new user
				user.UserName = "New User";

				// Ensure that the graph instance is new
				Assert.IsTrue(userInstance.IsNew, "Newly instance was not marked as new.");

				// Save the new user instance
				saveEvent = Perform<GraphSaveEvent>(() => userInstance.Save()).FirstOrDefault();

				// Commit the transaction to ensure it does not roll back changed to the model
				transaction.Commit();
			}

			// Ensure that the graph instance has been saved correctly
			Assert.IsFalse(userInstance.IsNew, "New instance was not saved.");
			Assert.IsTrue(saveEvent != null && saveEvent.Instance == userInstance && saveEvent.IdChanges.Count() == 1, 
				"The save event was not correctly raised during a save operation.");
		}

		/// <summary>
		/// Verify that <see cref="GraphContext.OnPropertyGet"/> is called when a property is accessed.
		///</summary>
		[TestMethod()]
		public virtual void OnPropertyGetTest()
		{
			var category = new TCategory();

			// Test getting a value property
			var propertyGet = Perform<GraphPropertyGetEvent>(() => NoOp(category.Name)).FirstOrDefault();
			Assert.IsNotNull(propertyGet, "The property get event was not raised for a value property.");
			Assert.AreEqual<string>("Name", propertyGet.Property.Name, "The name of the value property that was changed is incorrect.");
			Assert.AreEqual(category, propertyGet.Instance.Instance, "The instance the value property change occured on did not match the event");
			Assert.IsTrue(propertyGet.IsFirstAccess, "The event did not indicate that this was the first access for this value property.");

			// Test second access to a value property
			propertyGet = Perform<GraphPropertyGetEvent>(() => NoOp(category.Name)).FirstOrDefault();
			Assert.IsNotNull(propertyGet, "The property get event was not raised for a value property.");
			Assert.AreEqual<string>("Name", propertyGet.Property.Name, "The name of the value property that was changed is incorrect.");
			Assert.AreEqual(category, propertyGet.Instance.Instance, "The instance the value property change occured on did not match the event");
			Assert.IsFalse(propertyGet.IsFirstAccess, "The event did not indicate that this was not the first access for this value property.");

			// Test getting a reference property
			propertyGet = Perform<GraphPropertyGetEvent>(() => NoOp(category.ParentCategory)).FirstOrDefault();
			Assert.IsNotNull(propertyGet, "The property get event was not raised for a value property.");
			Assert.AreEqual<string>("ParentCategory", propertyGet.Property.Name, "The name of the value property that was changed is incorrect.");
			Assert.AreEqual(category, propertyGet.Instance.Instance, "The instance the value property change occured on did not match the event");
			Assert.IsTrue(propertyGet.IsFirstAccess, "The event did not indicate that this was the first access for this reference property.");

			// Test second access to a reference property
			propertyGet = Perform<GraphPropertyGetEvent>(() => NoOp(category.ParentCategory)).FirstOrDefault();
			Assert.IsNotNull(propertyGet, "The property get event was not raised for a value property.");
			Assert.AreEqual<string>("ParentCategory", propertyGet.Property.Name, "The name of the value property that was changed is incorrect.");
			Assert.AreEqual(category, propertyGet.Instance.Instance, "The instance the value property change occured on did not match the event");
			Assert.IsFalse(propertyGet.IsFirstAccess, "The event did not indicate that this was not the first access for this reference property.");

			// Test getting a list property
			propertyGet = Perform<GraphPropertyGetEvent>(() => NoOp(category.ChildCategories)).FirstOrDefault();
			Assert.IsNotNull(propertyGet, "The property get event was not raised for a value property.");
			Assert.AreEqual<string>("ChildCategories", propertyGet.Property.Name, "The name of the value property that was changed is incorrect.");
			Assert.AreEqual(category, propertyGet.Instance.Instance, "The instance the value property change occured on did not match the event");
			Assert.IsTrue(propertyGet.IsFirstAccess, "The event did not indicate that this was the first access for this list property.");

			// Test second access to a reference property
			propertyGet = Perform<GraphPropertyGetEvent>(() => NoOp(category.ChildCategories)).FirstOrDefault();
			Assert.IsNotNull(propertyGet, "The property get event was not raised for a value property.");
			Assert.AreEqual<string>("ChildCategories", propertyGet.Property.Name, "The name of the value property that was changed is incorrect.");
			Assert.AreEqual(category, propertyGet.Instance.Instance, "The instance the value property change occured on did not match the event");
			Assert.IsFalse(propertyGet.IsFirstAccess, "The event did not indicate that this was not the first access for this list property.");
		}

		// Stub to allow properties to be treated as actions
		void NoOp(object o)
		{ }

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
			var valueChange = Perform<GraphValueChangeEvent>(() => request.Description = "My New Description").FirstOrDefault();
			Assert.IsTrue(valueChange != null && valueChange.Property.Name == "Description" &&
				String.IsNullOrEmpty((string)valueChange.OldValue) &&	(string)valueChange.NewValue == "My New Description", 
				"Property change was not correctly raised on value property.");

			// Test a reference property
			var referenceChange = Perform<GraphReferenceChangeEvent>(() => request.AssignedTo = user).FirstOrDefault();
			Assert.IsTrue(referenceChange != null && referenceChange.Property.Name == "AssignedTo" &&
				referenceChange.OldValue == null &&	referenceChange.NewValue.Instance == (object)user, 
				"Property change was not correctly raised on reference property.");
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
			var listChange = Perform<GraphListChangeEvent>(() => user.Requests.Add(request)).FirstOrDefault();
			Assert.IsTrue(listChange != null && listChange.Property.Name == "Requests" &&
				listChange.Added.Any() && listChange.Added.FirstOrDefault().Instance == (object)request &&
				!listChange.Removed.Any(), 
				"List change was not correctly raised when adding an item.");

			// Test removing from a list
			listChange = Perform<GraphListChangeEvent>(() => user.Requests.Remove(request)).FirstOrDefault();
			Assert.IsTrue(listChange != null && listChange.Property.Name == "Requests" &&
				listChange.Removed.Any() && listChange.Removed.FirstOrDefault().Instance == (object)request &&
				!listChange.Added.Any(),
				"List change was not correctly raised when removing an item.");
	
			// Test clears
			user.Requests.Add(new TRequest());
			user.Requests.Add(new TRequest());
			user.Requests.Add(new TRequest());
			var listChanges = Perform<GraphListChangeEvent>(() => user.Requests.Clear());
			int removed = 0;
			foreach (var change in listChanges)
			{
				Assert.IsTrue(change.Property.Name == "Requests" && listChange.Removed.Any() && !listChange.Added.Any(),
				"List change was not correctly raised when clearing a list.");
				removed += listChange.Removed.Count();
			}
			Assert.AreEqual<int>(3, removed, "The clear operation did not raise change events for all of the items in the list.");
		}

		/// <summary>
		/// Verify that <see cref="GraphContext.OnInit"/> is called when a new instance is created.
		///</summary>
		[TestMethod()]
		public virtual void OnInitTest()
		{
			// Test init not raise just due to construction
			GraphInitEvent init = Perform<GraphInitEvent>(() => new TPriority()).FirstOrDefault();
			Assert.IsNull(init, "Init event was raised prematurely when a new object was constructed.");

			// Test init raised after first access
			init = Perform<GraphInitEvent>(() => NoOp(new TPriority().Name)).FirstOrDefault();
			Assert.IsNotNull(init, "Init event was not raised when a new object was constructed and accessed.");
	
		}

		/// <summary>
		/// Verifies that <see cref="GraphContext.GetInstance"/> returns a value new or existing instance.
		///</summary>
		[TestMethod()]
		public virtual void GetInstanceTest()
		{
			GraphType userType = GraphContext.Current.GetGraphType<TUser>();
			GraphType requestType = GraphContext.Current.GetGraphType<TRequest>();
			GraphType priorityType = GraphContext.Current.GetGraphType<TPriority>();
			GraphType categoryType = GraphContext.Current.GetGraphType<TCategory>();
			
			// Test creating new instance
			GraphInstance request = requestType.Create();
			Assert.IsNotNull(request, "Did not successfully create a new request instance.");
			Assert.IsTrue(request.Instance is TRequest, "Newly created request instance was not the correct type.");

			GraphInstance user = userType.Create();
			Assert.IsNotNull(user, "Did not successfully create a new user instance.");
			Assert.IsTrue(user.Instance is TUser, "Newly created user instance was not the correct type.");

			// Test creating existing instance
			GraphInstance category = categoryType.Create("1");
			Assert.IsNotNull(category, "Did not successfully create a existing category instance.");
			Assert.IsTrue(category.Instance is TCategory, "Existing category instance was not the correct type.");

			GraphInstance priority = priorityType.Create("1");
			Assert.IsNotNull(priority, "Did not successfully create a existing priority instance.");
			Assert.IsTrue(priority.Instance is TPriority, "Existing priority instance was not the correct type.");

			// Save a new instance in order to validate cached loading of existing instances that were just saved
			request["User"] = user.Instance;
			request["Category"] = category.Instance;
			request["Priority"] = priority.Instance;
			request["Description"] = "The is a test request";
			user["UserName"] = "Test User";
			request.Save();
			Assert.IsFalse(request.IsNew, "The request was not saved.");
			Assert.IsFalse(user.IsNew, "The user was not saved.");
			
			// Test getting cached reference to existing instance
			GraphInstance categoryClone = categoryType.Create("1");
			Assert.AreSame(category, categoryClone, "The categories have the same id but were not the same instance as expected.");

			GraphInstance requestClone = requestType.Create(request.Id);
			Assert.AreSame(request, requestClone, "The requests have the same id but were not the same instance as expected.");

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

		/// <summary>
		/// Performs the specified action and returns the last graph event raised as
		/// a result of the action or null if not events where raised or the last event
		/// was not of the specified event type.
		/// </summary>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="action"></param>
		/// <returns></returns>
		IEnumerable<TEvent> Perform<TEvent>(Action action)
			where TEvent : GraphEvent
		{
			events = new List<GraphEvent>();
			try
			{
				GraphContext.Current.Event += UpdateLastEvent;
				action();
				return events.OfType<TEvent>();
			}
			finally
			{
				GraphContext.Current.Event -= UpdateLastEvent;
			}
		}

		List<GraphEvent> events;

		void UpdateLastEvent(object sender, GraphEvent e)
		{
			events.Add(e);
		}
	}
}
