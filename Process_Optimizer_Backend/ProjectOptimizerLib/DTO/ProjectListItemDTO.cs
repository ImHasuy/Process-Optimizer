using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ProjectListItemDTO
	{
		public Guid Id { get; init; }
		public string Name { get; init; } = string.Empty;
		public ETimeUnit TimeUnit { get; init; }
		public string Currency { get; init; } = string.Empty;
		public int LatestRevisionNumber { get; init; }
		public bool IsOptimizable { get; init; }
		public decimal? MaxCost { get; init; }
		public decimal? MaxDurationBound { get; init; }
		public DateTime CreatedAt { get; init; }
		public DateTime UpdatedAt { get; init; }
	}
}
