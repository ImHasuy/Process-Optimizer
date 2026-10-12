using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ProjectDetailsDTO
	{
		public Guid Id { get; init; }
		public string Name { get; init; } = string.Empty;
		public string? Description { get; init; }
		public ETimeUnit TimeUnit { get; init; }
		public string Currency { get; init; } = string.Empty;
		public DateTime CreatedAt { get; init; }
		public DateTime UpdatedAt { get; init; }
		public decimal? MaxCost { get; init; }
		public decimal? MaxDurationBound { get; init; }
		public DiagramSummaryDTO Diagram { get; init; } = null!;
	}
}
