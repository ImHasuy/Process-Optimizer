namespace ProjectOptimizerLib.DTO
{
	public class ApiResponse
	{
		public int StatusCode { get; init; }
		public string Message { get; init; } = string.Empty;
		public DateTime Date { get; init; } = DateTime.UtcNow;
		public IDictionary<string, string[]>? Errors { get; init; }
	}

	public sealed class ApiResponse<TData> : ApiResponse
	{
		public TData? Data { get; init; }
	}
}
