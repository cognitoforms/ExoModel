namespace ExoModel.UnitTests.Models
{
	/// <summary>
	/// Determines how test model data should be stored.
	/// </summary>
	public enum TestModelStorageMode
	{
		/// <summary>
		/// The model data storage strategy is not specified.
		/// </summary>
		Unspecified,

		/// <summary>
		/// Model data should be stored in a permanent location
		/// that can be retrieved in the future.
		/// </summary>
		Permanent,

		/// <summary>
		/// Model data should be stored in a temporary location.
		/// </summary>
		Temporary,
	}
}
