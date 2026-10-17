using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ScenarioActivityDTO
	{
		public string BpmnId { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public EFlowNodeKind Kind { get; init; }
		public decimal Duration { get; init; }
		public decimal Cost { get; init; }
		public bool Parallelizable { get; init; }
		public decimal EarliestStart { get; init; }
		public decimal EarliestFinish { get; init; }
		public decimal LatestStart { get; init; }
		public decimal LatestFinish { get; init; }
		public decimal Slack { get; init; }
		public bool IsCritical { get; init; }
	}
}
