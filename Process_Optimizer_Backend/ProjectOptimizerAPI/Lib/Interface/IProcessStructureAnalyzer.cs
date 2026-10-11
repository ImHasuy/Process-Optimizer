using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IProcessStructureAnalyzer
	{
		StructureAnalysisResult Analyze(ProcessGraph graph);
	}
}
