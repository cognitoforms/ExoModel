using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;

namespace ExoModel.UnitTest.Models.Movies
{
	[ModelFormat("[Name]")]
	public class Genre : NamedItem
	{
		public static ICollection<Genre> All
		{
			get { return All<Genre>(); }
		}
	}
}
