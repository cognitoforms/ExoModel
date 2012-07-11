using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExoModel;
using System.ComponentModel.DataAnnotations;

namespace ExoModel.UnitTest.Models.Movies
{
	[ModelFormat("[Name] ([Year])")]
	public class Movie : NamedItem
	{
		public static ICollection<Movie> All
		{
			get { return All<Movie>(); }
		}

		public int Year
		{
			get { return Get(() => Year); }
			set { Set(() => Year, value); }
		}

		public string Rated
		{
			get { return Get(() => Rated); }
			set { Set(() => Rated, value); }
		}

		[DisplayFormat(DataFormatString="d")]
		public DateTime Released
		{
			get { return Get(() => Released); }
			set { Set(() => Released, value); }
		}

		public ICollection<Genre> Genres
		{
			get { return Get(() => Genres); }
			set { Set(() => Genres, value); }
		}

		public Director Director
		{
			get { return Get(() => Director); }
			set { Set(() => Director, value); }
		}

		public ICollection<Role> Roles
		{
			get { return Get(() => Roles); }
			set { Set(() => Roles, value); }
		}

		public string PosterUrl
		{
			get { return Get(() => PosterUrl); }
			set { Set(() => PosterUrl, value); }
		}
	}
}
