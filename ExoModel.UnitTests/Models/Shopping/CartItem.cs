using System.ComponentModel.DataAnnotations;
using ExoModel.Json;

namespace ExoModel.UnitTests.Models.Shopping
{
	[ModelFormat("[Item.Product.Name] x [Quantity] @ [Item.Price]")]
	public class CartItem : JsonEntity
	{
		public Cart Cart { get; set; }

		public Item Item { get; set; }

		[DisplayFormat(DataFormatString = null)]
		public int Quantity { get; set; }
	}
}
