using System.Collections.Generic;
using ExoModel.Json;

namespace ExoModel.UnitTests.Models.Shopping
{
	public class Cart : JsonEntity
	{
		public ICollection<CartItem> Items { get; set; } 
	}
}
