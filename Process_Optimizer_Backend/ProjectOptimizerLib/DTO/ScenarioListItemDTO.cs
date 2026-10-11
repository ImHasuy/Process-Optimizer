namespace ProjectOptimizerLib.DTO
{
	public sealed class ScenarioListItemDTO
	{
		public long Index { get; init; }
		public string Key { get; init; } = string.Empty;
		public List<ScenarioChoiceDTO> Choices { get; init; } = new();
		public int ActivityCount { get; init; }
		public decimal TotalDuration { get; init; }
		public decimal TotalCost { get; init; }
	}
}
