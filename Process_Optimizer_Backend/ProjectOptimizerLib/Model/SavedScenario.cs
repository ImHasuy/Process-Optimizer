namespace ProjectOptimizerLib.Model
{
	public class SavedScenario
	{
		public Guid Id { get; set; }
		public Guid ProjectId { get; set; }
		public Project Project { get; set; } = null!;
		public string Name { get; set; } = string.Empty;
		public string NormalizedName { get; set; } = string.Empty;
		public string? Note { get; set; }
		public int RevisionNumber { get; set; }
		public long ScenarioIndex { get; set; }
		public int OrderingVersion { get; set; }
		public string ScenarioKey { get; set; } = string.Empty;
		public string SnapshotJson { get; set; } = string.Empty;
		public decimal TotalDuration { get; set; }
		public decimal TotalCost { get; set; }
		public DateTime CreatedAt { get; set; }
	}
}
