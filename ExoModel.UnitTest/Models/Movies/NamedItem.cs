using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.UnitTest.Models.Movies
{
	[ModelFormat("[Name]")]
	public class NamedItem : TestEntity
	{
		public string Name
		{
			get { return Get(() => Name); }
			set { Set(() => Name, value); }
		}
	}
}
