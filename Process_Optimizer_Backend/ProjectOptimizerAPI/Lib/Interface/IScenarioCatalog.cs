using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IScenarioCatalog
	{
		Task<AnalyzedRevision> AnalyzeRevisionAsync(Guid projectId, int? revisionNumber);
		Task<AnalyzedRevision> GetOptimizableRevisionAsync(Guid projectId, int? revisionNumber);
		Task<IndexedRevision> GetIndexedRevisionAsync(Guid projectId, int? revisionNumber);
		Task<Func<string, long?>> CreateLatestRevisionRankerAsync(Guid projectId);
	}
}
