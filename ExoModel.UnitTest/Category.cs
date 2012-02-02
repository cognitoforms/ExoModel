using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace ExoGraph.UnitTest
{
	public class Category : TestEntity, ICategory<User, Category, Priority, Request, ICollection<Request>, ICollection<Category>>
	{
		public string Name
		{
			get { return Get(() => Name); }
			set { Set(() => Name, value); }
		}

		public Category ParentCategory
		{
			get { return Get(() => ParentCategory); }
			set { Set(() => ParentCategory, value); }
		}

		public ICollection<Category> ChildCategories
		{
			get { return Get(() => ChildCategories); }
			set { Set(() => ChildCategories, value); }
		}
	}
}
