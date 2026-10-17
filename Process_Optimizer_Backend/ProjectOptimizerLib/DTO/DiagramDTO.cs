namespace ProjectOptimizerLib.DTO
{
	public sealed class DiagramDTO
	{
		public DateTime UpdatedAt { get; init; }
		public string BpmnXml { get; init; } = string.Empty;
		public string ContentSha256 { get; init; } = string.Empty;
		public int SizeBytes { get; init; }
		public int FlowNodeCount { get; init; }
		public int ErrorCount { get; init; }
		public int WarningCount { get; init; }
		public bool IsOptimizable { get; init; }
	}
}
