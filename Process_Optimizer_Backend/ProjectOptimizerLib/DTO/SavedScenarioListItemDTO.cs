namespace ProjectOptimizerLib.DTO
{
	public sealed record SavedScenarioListItemDTO
	{
		public Guid Id { get; init; }
		public string Name { get; init; } = string.Empty;
		public int RevisionNumber { get; init; }
		public string ScenarioKey { get; init; } = string.Empty;
		public decimal TotalDuration { get; init; }
		public decimal TotalCost { get; init; }
		public bool IsAvailableInLatestRevision { get; init; }
		public DateTime CreatedAt { get; init; }
	}
}
