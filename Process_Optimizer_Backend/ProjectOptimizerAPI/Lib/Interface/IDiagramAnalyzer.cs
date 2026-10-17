using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IDiagramAnalyzer
	{
		DiagramAnalysis Analyze(string contentSha256, string bpmnXml);
		ScenarioComputation GetScenario(DiagramAnalysis analysis, long index);
	}
}
