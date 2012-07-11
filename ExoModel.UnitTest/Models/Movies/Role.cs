using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.UnitTest.Models.Movies
{
	[ModelFormat("[Actor] played [Name] in [Movie]")]
	public class Role : TestEntity
	{
		public Actor Actor
		{
			get { return Get(() => Actor); }
			set { Set(() => Actor, value); }
		}
		
		public Movie Movie
		{
			get { return Get(() => Movie); }
			set { Set(() => Movie, value); }
		}

		public string Name
		{
			get { return Get(() => Name); }
			set { Set(() => Name, value); }
		}

		public int Order
		{
			get { return Get(() => Order); }
			set { Set(() => Order, value); }
		}

		public bool Star
		{
			get { return Get(() => Star); }
			set { Set(() => Star, value); }
		}

		public bool Lead
		{
			get { return Get(() => Lead); }
			set { Set(() => Lead, value); }
		}
	}
}
