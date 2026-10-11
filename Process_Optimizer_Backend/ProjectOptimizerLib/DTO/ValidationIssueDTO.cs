using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ValidationIssueDTO
	{
		public string Code { get; init; } = string.Empty;
		public EIssueSeverity Severity { get; init; }
		public string? BpmnElementId { get; init; }
		public string Message { get; init; } = string.Empty;
	}
}
