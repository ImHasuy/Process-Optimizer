namespace ProjectOptimizerLib.DTO
{
	public sealed class DiagramRevisionListItemDTO
	{
		public int RevisionNumber { get; init; }
		public DateTime CreatedAt { get; init; }
		public int SizeBytes { get; init; }
		public int FlowNodeCount { get; init; }
		public int ErrorCount { get; init; }
		public int WarningCount { get; init; }
		public bool IsOptimizable { get; init; }
	}
}
