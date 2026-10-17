namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record ScenarioComputation(
		ScenarioSelection Selection,
		IReadOnlyList<string> ElementIds,
		IReadOnlyList<string> SequenceFlowIds,
		IReadOnlyList<ScheduledNode> Activities,
		IReadOnlyList<string> CriticalPath,
		long PathCount,
		decimal TotalDuration,
		decimal SequentialDuration,
		decimal TotalCost,
		bool HasExclusiveActivities,
		bool HasMissingEstimates);
}
