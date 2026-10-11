namespace ProjectOptimizerLib.DTO
{
	public sealed class ValidationReportDTO
	{
		public int RevisionNumber { get; init; }
		public bool IsOptimizable { get; init; }
		public List<ValidationIssueDTO> Issues { get; init; } = new();
	}
}
