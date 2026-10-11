namespace ProjectOptimizerAPI.Lib.Helper
{
	public sealed record PageRequest(int Page, int PageSize)
	{
		public const int DefaultPage = 1;
		public const int DefaultPageSize = 20;

		public long Offset => (long)(Page - 1) * PageSize;

		public static PageRequest Resolve(int? page, int? pageSize) => new(page ?? DefaultPage, pageSize ?? DefaultPageSize);
	}
}
