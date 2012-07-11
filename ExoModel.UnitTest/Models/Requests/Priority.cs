using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace ExoModel.UnitTest
{
	public class Priority : TestEntity, IPriority<User, Category, Priority, Request, ICollection<Request>, ICollection<Category>>
	{
		public string Name
		{
			get { return Get(() => Name); }
			set { Set(() => Name, value); }
		}
	}
}
