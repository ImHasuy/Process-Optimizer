namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record Estimate(
		decimal Optimistic,
		decimal MostLikely,
		decimal Pessimistic);
}
