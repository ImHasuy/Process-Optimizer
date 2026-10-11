using ProjectOptimizerAPI.Lib.Analysis;

namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record IndexedRevision(
		ResolvedRevision Revision,
		DiagramAnalysis Analysis,
		long Count)
	{
		public bool Contains(long index) => AnalysisLimits.IsIndexInRange(index, Count);
	}
}
