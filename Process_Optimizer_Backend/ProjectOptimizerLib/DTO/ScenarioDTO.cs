using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ScenarioDTO
	{
		public Guid ProjectId { get; init; }
		public int OrderingVersion { get; init; }
		public long Index { get; init; }
		public long Count { get; init; }
		public string Key { get; init; } = string.Empty;
		public List<ScenarioChoiceDTO> Choices { get; init; } = new();
		public List<string> ElementIds { get; init; } = new();
		public List<string> SequenceFlowIds { get; init; } = new();
		public List<ScenarioActivityDTO> Activities { get; init; } = new();
		public List<string> CriticalPath { get; init; } = new();
		public long PathCount { get; init; }
		public decimal TotalDuration { get; init; }
		public decimal SequentialDuration { get; init; }
		public decimal TotalCost { get; init; }
		public bool HasExclusiveActivities { get; init; }
		public bool HasMissingEstimates { get; init; }
		public ETimeUnit TimeUnit { get; init; }
		public string Currency { get; init; } = string.Empty;
	}
}
