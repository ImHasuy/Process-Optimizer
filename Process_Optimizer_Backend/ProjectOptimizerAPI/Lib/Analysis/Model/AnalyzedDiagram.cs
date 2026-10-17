namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record AnalyzedDiagram(
		ResolvedDiagram Diagram,
		DiagramAnalysis Analysis);
}
