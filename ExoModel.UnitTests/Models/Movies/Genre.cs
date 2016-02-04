using ExoModel.Json;

namespace ExoModel.UnitTests.Models.Movies
{
	[ModelFormat("[Name]")]
	public class Genre : JsonEntity
	{
		public string Name { get; set; }
	}
}
