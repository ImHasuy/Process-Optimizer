namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record CriticalPathResult(
		IReadOnlyList<ScheduledNode> Schedule,
		IReadOnlyList<string> CriticalPath,
		decimal Makespan);
}
