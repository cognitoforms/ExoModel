using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.UnitTest.Models.Movies
{
	[ModelFormat("[Person]")]
	public class Actor : TestEntity
	{
		public static ICollection<Actor> All
		{
			get { return All<Actor>(); }
		}

		public Person Person
		{
			get { return Get(() => Person); }
			set { Set(() => Person, value); }
		}

		public ICollection<Role> Roles
		{
			get { return Get(() => Roles); }
			set { Set(() => Roles, value); }
		}

		public string BioPreview
		{
			get { return Get(() => BioPreview); }
			set { Set(() => BioPreview, value); }
		}

		public string Bio
		{
			get { return Get(() => Bio); }
			set { Set(() => Bio, value); }
		}
	}
}
