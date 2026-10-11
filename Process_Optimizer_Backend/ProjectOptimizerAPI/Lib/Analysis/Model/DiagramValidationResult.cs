namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record DiagramValidationResult(
		IReadOnlyList<ValidationIssue> Issues,
		bool IsOptimizable,
		StructureFragment? Structure);
}
