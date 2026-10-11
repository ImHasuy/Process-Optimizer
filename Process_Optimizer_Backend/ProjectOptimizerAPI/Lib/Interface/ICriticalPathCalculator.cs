using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface ICriticalPathCalculator
	{
		CriticalPathResult Calculate(IReadOnlyList<FlowNode> orderedNodes, IReadOnlyList<SequenceFlowEdge> edges);
	}
}
