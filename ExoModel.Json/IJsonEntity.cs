namespace ExoModel.Json
{
	/// <summary>
	/// TODO
	/// </summary>
	public interface IJsonEntity
	{
		/// <summary>
		/// Gets the instance's id.
		/// </summary>
		int? Id { get; set; }

		/// <summary>
		/// Gets whether or not the instance is initialized.
		/// </summary>
		bool? IsInitialized { get; set; }
	}
}
