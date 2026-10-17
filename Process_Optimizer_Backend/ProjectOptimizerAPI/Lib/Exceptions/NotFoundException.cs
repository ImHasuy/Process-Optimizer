namespace ProjectOptimizerAPI.Lib.Exceptions
{
	public sealed class NotFoundException(string message) : ApiException(StatusCodes.Status404NotFound, message)
	{
		public static NotFoundException For<TEntity>(Guid id) => new($"{typeof(TEntity).Name} with id '{id}' was not found.");
	}
}
