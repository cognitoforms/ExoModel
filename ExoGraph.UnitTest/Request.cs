using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace ExoGraph.UnitTest
{
	public class Request : TestEntity, IRequest<User, Category, Priority, Request, ICollection<Request>, ICollection<Category>>
	{
		public User User
		{
			get { return Get(() => User); }
			set { Set(() => User, value); }
		}

		public Category Category
		{
			get { return Get(() => Category); }
			set { Set(() => Category, value); }
		}

		public Priority Priority
		{
			get { return Get(() => Priority); }
			set { Set(() => Priority, value); }
		}

		public string Description
		{
			get { return Get(() => Description); }
			set { Set(() => Description, value); }
		}

		public User AssignedTo
		{
			get { return Get(() => AssignedTo); }
			set { Set(() => AssignedTo, value); }
		}
	}
}
