namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record ScheduledNode(
		FlowNode Node,
		decimal EarliestStart,
		decimal EarliestFinish,
		decimal LatestStart,
		decimal LatestFinish,
		decimal Slack,
		bool IsCritical);
}
