namespace ProjectOptimizerLib.DTO
{
	public sealed class PagedResultDTO<T>
	{
		public List<T> Items { get; init; } = new();
		public int Page { get; init; }
		public int PageSize { get; init; }
		public long TotalCount { get; init; }
	}
}
