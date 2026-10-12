namespace ProjectOptimizerLib.DTO
{
	public sealed record SavedScenarioListItemDTO
	{
		public Guid Id { get; init; }
		public string Name { get; init; } = string.Empty;
		public string ScenarioKey { get; init; } = string.Empty;
		public decimal TotalDuration { get; init; }
		public decimal TotalCost { get; init; }
		public bool IsAvailable { get; init; }
		public DateTime CreatedAt { get; init; }
	}
}
