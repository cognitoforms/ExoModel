﻿using System.Collections.Generic;
using ExoModel.Json;

namespace ExoModel.UnitTests.Models.Movies
{
	[ModelFormat("[Person]")]
	public class Actor : JsonEntity
	{
		public Person Person { get; set; }

		public ICollection<Role> Roles { get; set; }

		public string BioPreview { get; set; }

		public string Bio { get; set; }
	}
}
