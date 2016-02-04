using System.Collections.Generic;
using ExoModel.Json;

namespace ExoModel.UnitTests.Models.Movies
{
	[ModelFormat("[Person.FirstName] [Person.LastName]")]
	public class Director : JsonEntity
	{
		public Person Person { get; set; }

		public ICollection<Movie> Movies { get; set; }
	}
}
