namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record AnalyzedRevision(
		ResolvedRevision Revision,
		DiagramAnalysis Analysis);
}
