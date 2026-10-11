namespace ProjectOptimizerAPI.Lib.Exceptions
{
	public abstract class ApiException(int statusCode, string message) : Exception(message)
	{
		public int StatusCode { get; } = statusCode;
	}
}
