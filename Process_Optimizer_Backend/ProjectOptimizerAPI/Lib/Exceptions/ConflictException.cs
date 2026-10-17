namespace ProjectOptimizerAPI.Lib.Exceptions
{
	public sealed class ConflictException(string message) : ApiException(StatusCodes.Status409Conflict, message);
}
