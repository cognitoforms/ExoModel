namespace ExoModel
{
	/// <summary>
	/// Interface for providers that handle creation and storage of <see cref="ModelContext"/>
	/// implementations for an application.
	/// </summary>
	/// <remarks>
	/// The <see cref="ModelContext.Provider"/> property must be set at application startup
	/// to the appropriate implementation of this interface.  The singleton will be accessed through
	/// the <see cref="ModelContext.Current"/> property to expose the appropriate context for
	/// the current thread of execution.  Implementations will be responsible for performing both
	/// the appropriate initialization and storage of the <see cref="ModelContext"/>.
	/// </remarks>
	public interface IModelContextProvider
	{
		ModelContext Context { get; set; }
	}
}
