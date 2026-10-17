using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ResourceLimitsDTO
	{
		public Guid ProjectId { get; init; }
		public EResourceLimitBasis Basis { get; init; }
		public ETimeUnit TimeUnit { get; init; }
		public string Currency { get; init; } = string.Empty;
		public long? ScenarioCount { get; init; }
		public decimal MaxCost { get; init; }
		public ScenarioRefDTO? MaxCostScenario { get; init; }
		public decimal MinCost { get; init; }
		public ScenarioRefDTO? MinCostScenario { get; init; }
		public decimal MaxCriticalPathDuration { get; init; }
		public ScenarioRefDTO? MaxCriticalPathScenario { get; init; }
		public decimal MinCriticalPathDuration { get; init; }
		public ScenarioRefDTO? MinCriticalPathScenario { get; init; }
		public decimal MaxExclusiveDuration { get; init; }
		public decimal MaxDurationBound { get; init; }
		public decimal MaxSequentialDuration { get; init; }
		public ScenarioRefDTO? MaxSequentialScenario { get; init; }
		public bool IsJointWorstCase { get; init; }
		public bool HasExclusiveActivities { get; init; }
		public bool HasMissingEstimates { get; init; }
		public List<string> Notes { get; init; } = new();
	}
}
