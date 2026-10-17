namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record NodeNetwork(
		IReadOnlyList<FlowNode> Nodes,
		IReadOnlyDictionary<string, int> Positions,
		IReadOnlyList<(int Source, int Target)> Links,
		IReadOnlyList<IReadOnlyList<int>> Predecessors,
		IReadOnlyList<IReadOnlyList<int>> Successors)
	{
		public static NodeNetwork Create(IReadOnlyList<FlowNode> orderedNodes, IEnumerable<SequenceFlowEdge> edges)
		{
			var positions = orderedNodes
				.Select((node, position) => (node.BpmnId, position))
				.ToDictionary(entry => entry.BpmnId, entry => entry.position, StringComparer.Ordinal);
			var links = edges
				.Where(edge => positions.ContainsKey(edge.SourceId) && positions.ContainsKey(edge.TargetId))
				.Select(edge => (Source: positions[edge.SourceId], Target: positions[edge.TargetId]))
				.Distinct()
				.ToList();
			return new NodeNetwork(
				orderedNodes,
				positions,
				links,
				BuildAdjacency(orderedNodes.Count, links, link => link.Target, link => link.Source),
				BuildAdjacency(orderedNodes.Count, links, link => link.Source, link => link.Target));
		}

		public IEnumerable<int> Sinks() => Enumerable.Range(0, Nodes.Count).Where(position => Successors[position].Count == 0);

		private static IReadOnlyList<IReadOnlyList<int>> BuildAdjacency(int nodeCount, IReadOnlyList<(int Source, int Target)> links, Func<(int Source, int Target), int> keySelector, Func<(int Source, int Target), int> valueSelector)
		{
			var lookup = links.ToLookup(keySelector, valueSelector);
			return Enumerable.Range(0, nodeCount).Select(position => (IReadOnlyList<int>)lookup[position].Order().ToList()).ToList();
		}
	}
}
