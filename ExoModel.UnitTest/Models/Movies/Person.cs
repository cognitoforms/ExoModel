using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.UnitTest.Models.Movies
{
	[ModelFormat("[FirstName] [LastName]")]
	public class Person : TestEntity
	{
		public static ICollection<Person> All
		{
			get { return All<Person>(); }
		}

		public string FirstName
		{
			get { return Get(() => FirstName); }
			set { Set(() => FirstName, value); }
		}

		public string LastName
		{
			get { return Get(() => LastName); }
			set { Set(() => LastName, value); }
		}

		public string PhotoUrl
		{
			get { return Get(() => PhotoUrl); }
			set { Set(() => PhotoUrl, value); }
		}

		public Actor Actor
		{
			get { return Get(() => Actor); }
			set { Set(() => Actor, value); }
		}

		public Director Director
		{
			get { return Get(() => Director); }
			set { Set(() => Director, value); }
		}
	}
}
