namespace ProjectOptimizerLib.DTO
{
	public sealed class EstimateDTO
	{
		public decimal Optimistic { get; init; }
		public decimal MostLikely { get; init; }
		public decimal Pessimistic { get; init; }
	}
}
