using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IDiagramAnalyzer
	{
		DiagramAnalysis Analyze(string contentSha256, string bpmnXml);
		ScenarioComputation GetScenario(DiagramAnalysis analysis, long index);
		IReadOnlyList<long> GetSortedScenarioIndices(DiagramAnalysis analysis, EScenarioSortBy sortBy, ESortDirection direction);
	}
}
