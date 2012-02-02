using System;
using System.Collections.Generic;
using System.Text;
using ExoGraph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExoGraph.UnitTest
{
	/// <summary>
	/// Base class for unit tests that validate difference <see cref="GraphContext"/> subclass implementations.
	/// </summary>
	[TestClass]
	public class ContextTestBase
	{
		Dictionary<string, GraphEvent> events;

		/// <summary>
		/// Subscribes to all events raised by ExoGraph.
		/// </summary>
		void SubscribeToEvents()
		{
			// Create a new event dictionary to track event occurrences
			events = new Dictionary<string, GraphEvent>();

			// Obtain graph type for customer and contact types
			GraphType customerType = GraphContext.Current.GetGraphType("Customer");
			GraphType contactType = GraphContext.Current.GetGraphType("Contact");

			// Subscribe to init event
			customerType.Init +=
				(sender, e) => events["Init"] = e;

			// Subscribe to property get event
			customerType.PropertyGet +=
				(sender, e) => events["PropertyGet"] = e;

			// Subscribe to reference change event
			customerType.ReferenceChange +=
				(sender, e) => events["ReferenceChange"] = e;

			// Subscribe to value change event
			customerType.ValueChange +=
				(sender, e) => events["ValueChange"] = e;

			// Subscribe list change event
			customerType.ListChange +=
				(sender, e) => events["ListChange"] = e;

			// Subscribe to path change event
			customerType.GetPath("PrimaryContact.Name").Change +=
				(sender, e) => events["PathChange"] = null;

			// Subscribe to a custom event
			customerType.Subscribe<CustomEvent>(
				(instance, customEvent) => events["CustomEvent"] = null);
		}

		/// <summary>
		/// Validate that all of the events were raised.
		/// </summary>
		void ValidateEvents()
		{
			if (!events.ContainsKey("Init"))
				Assert.Fail("The Init event was not raised.");
			if (!events.ContainsKey("PropertyGet"))
				Assert.Fail("The PropertyGet event was not raised.");
			if (!events.ContainsKey("ReferenceChange"))
				Assert.Fail("The ReferenceChange event was not raised.");
			if (!events.ContainsKey("ValueChange"))
				Assert.Fail("The ValueChange event was not raised.");
			if (!events.ContainsKey("ListChange"))
				Assert.Fail("The ListChange event was not raised.");
			if (!events.ContainsKey("PathChange"))
				Assert.Fail("The PathChange event was not raised.");
			if (!events.ContainsKey("CustomEvent"))
				Assert.Fail("The CustomEvent event was not raised.");
		}

		/// <summary>
		/// Perform a manipulation of the graph that raises all ExoGraph events.
		/// </summary>
		[TestMethod]
		public void TestGraphManipulation()
		{
			// Subscribe to events
			SubscribeToEvents();

			GraphInstance customer = null;
			GraphInstance contact = null;

			// Transact the entire test and implicitly roll back changes by not committing the transaction
			using (GraphContext.Current.BeginTransaction())
			{
				// Obtain graph type for customer and contact types
				GraphType customerType = GraphContext.Current.GetGraphType("Customer");
				GraphType contactType = GraphContext.Current.GetGraphType("Contact");

				// Create a customer
				customer = customerType.Create();

				// Change the customer name
				customer["Name"] = "New Customer Name";

				// Create a primary contact
				contact = contactType.Create();
				customer["PrimaryContact"] = contact.Instance;

				// Change the path
				contact["Name"] = "New Contact Name";

				// Add an additional contact
				contact = contactType.Create();
				customer.GetList("OtherContacts").Add(contact);

				// Raise a custom event
				customer.RaiseEvent<CustomEvent>(new CustomEvent());

				// Commit the instances
				customer.Save();
			}

			// Verify that rollback occurred successfully
			Assert.IsNull(customer["PrimaryContact"]);
			Assert.IsNull(contact["Name"]);

			// Verify that all events were appropriately raised
			ValidateEvents();
		}
	}

	/// <summary>
	/// Stub class representing a custom event type.
	/// </summary>
	public class CustomEvent
	{ }
}
