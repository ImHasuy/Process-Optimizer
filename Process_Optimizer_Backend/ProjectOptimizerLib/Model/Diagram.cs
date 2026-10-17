namespace ProjectOptimizerLib.Model
{
	/// <summary>The current diagram of a project. Each project has exactly one; saving overwrites it.</summary>
	public class Diagram
	{
		public Guid ProjectId { get; set; }
		public Project Project { get; set; } = null!;
		public string BpmnXml { get; set; } = string.Empty;
		public string ContentSha256 { get; set; } = string.Empty;
		public int SizeBytes { get; set; }
		public int FlowNodeCount { get; set; }
		public int ErrorCount { get; set; }
		public int WarningCount { get; set; }
		public bool IsOptimizable { get; set; }
		public decimal? MaxCost { get; set; }
		public decimal? MaxDurationBound { get; set; }
		public DateTime UpdatedAt { get; set; }
	}
}
