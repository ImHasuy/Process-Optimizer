namespace ProjectOptimizerLib.DTO
{
	public sealed class SavedScenarioDTO
	{
		public Guid Id { get; init; }
		public string Name { get; init; } = string.Empty;
		public string? Note { get; init; }
		public long ScenarioIndex { get; init; }
		public int OrderingVersion { get; init; }
		public string ScenarioKey { get; init; } = string.Empty;
		public DateTime CreatedAt { get; init; }
		public bool IsAvailable { get; init; }
		public long? CurrentIndex { get; init; }
		public ScenarioDTO Snapshot { get; init; } = null!;
	}
}
