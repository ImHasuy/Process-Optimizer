namespace ProjectOptimizerLib.DTO
{
	public sealed class DiagramSummaryDTO
	{
		public DateTime UpdatedAt { get; init; }
		public int SizeBytes { get; init; }
		public int FlowNodeCount { get; init; }
		public int ErrorCount { get; init; }
		public int WarningCount { get; init; }
		public bool IsOptimizable { get; init; }
	}
}
