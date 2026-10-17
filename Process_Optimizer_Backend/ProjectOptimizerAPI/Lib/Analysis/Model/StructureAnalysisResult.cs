namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record StructureAnalysisResult(
		StructureFragment? Root,
		IReadOnlyList<ValidationIssue> Issues);
}
