using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record ValidationIssue(
		string Code,
		EIssueSeverity Severity,
		string? BpmnElementId,
		string Message);
}
