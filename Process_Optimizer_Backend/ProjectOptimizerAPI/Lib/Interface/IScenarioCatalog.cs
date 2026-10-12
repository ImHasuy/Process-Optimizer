using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IScenarioCatalog
	{
		Task<AnalyzedDiagram> GetOptimizableDiagramAsync(Guid projectId);
		Task<IndexedDiagram> GetIndexedDiagramAsync(Guid projectId);
		Task<Func<string, long?>> CreateRankerAsync(Guid projectId);
	}
}
