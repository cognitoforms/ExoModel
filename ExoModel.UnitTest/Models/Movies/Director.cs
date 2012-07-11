using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.UnitTest.Models.Movies
{
	[ModelFormat("[Person.FirstName] [Person.LastName]")]
	public class Director : TestEntity
	{
		public static ICollection<Director> All
		{
			get { return All<Director>(); }
		}

		public Person Person
		{
			get { return Get(() => Person); }
			set { Set(() => Person, value); }
		}

		public ICollection<Movie> Movies
		{
			get { return Get(() => Movies); }
			set { Set(() => Movies, value); }
		}
	}
}
