namespace ProjectOptimizerAPI.Lib.Exceptions
{
	public sealed class InvalidRequestException(string message) : ApiException(StatusCodes.Status400BadRequest, message);
}
