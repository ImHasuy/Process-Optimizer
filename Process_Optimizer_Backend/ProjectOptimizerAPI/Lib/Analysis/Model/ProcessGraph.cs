namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record ProcessGraph(
		string ProcessId,
		IReadOnlyList<FlowNode> Nodes,
		IReadOnlyList<SequenceFlowEdge> Edges,
		IReadOnlyDictionary<string, FlowNode> NodesById,
		int FlowNodeCount,
		IReadOnlyList<ValidationIssue> ParseIssues);
}
