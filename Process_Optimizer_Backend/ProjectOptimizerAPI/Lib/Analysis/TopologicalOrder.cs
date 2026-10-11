using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public static class TopologicalOrder
	{
		private const int StartEventRank = 0;
		private const int InnerNodeRank = 1;
		private const int EndEventRank = 2;

		private static readonly Comparer<FlowNode> ReadyNodeComparer = Comparer<FlowNode>.Create(CompareReadyNodes);

		public static IReadOnlyList<FlowNode>? Sort(IReadOnlyCollection<FlowNode> nodes, IReadOnlyCollection<SequenceFlowEdge> edges)
		{
			var ordered = SortAcyclicPart(nodes, edges);
			return ordered.Count == nodes.Count ? ordered : null;
		}

		public static IReadOnlyList<FlowNode> SortAcyclicPart(IReadOnlyCollection<FlowNode> nodes, IReadOnlyCollection<SequenceFlowEdge> edges)
		{
			var nodesById = nodes.ToDictionary(node => node.BpmnId, StringComparer.Ordinal);
			var successors = ToDistinctSuccessors(nodesById, edges);
			var remainingIncoming = CountIncoming(successors);
			var ready = new PriorityQueue<FlowNode, FlowNode>(ReadyNodeComparer);
			EnqueueAll(ready, nodes.Where(node => !remainingIncoming.ContainsKey(node.BpmnId)));
			var ordered = new List<FlowNode>(nodes.Count);
			while (ready.TryDequeue(out var node, out _))
			{
				ordered.Add(node);
				EnqueueAll(ready, ReleaseSuccessors(node, successors, remainingIncoming).Select(targetId => nodesById[targetId]));
			}
			return ordered;
		}

		private static ILookup<string, string> ToDistinctSuccessors(IReadOnlyDictionary<string, FlowNode> nodesById, IEnumerable<SequenceFlowEdge> edges) =>
			edges
				.Where(edge => nodesById.ContainsKey(edge.SourceId) && nodesById.ContainsKey(edge.TargetId))
				.Select(edge => (edge.SourceId, edge.TargetId))
				.Distinct()
				.ToLookup(link => link.SourceId, link => link.TargetId, StringComparer.Ordinal);

		private static Dictionary<string, int> CountIncoming(ILookup<string, string> successors) =>
			successors
				.SelectMany(targets => targets)
				.GroupBy(targetId => targetId, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

		private static List<string> ReleaseSuccessors(FlowNode node, ILookup<string, string> successors, Dictionary<string, int> remainingIncoming)
		{
			var released = new List<string>();
			foreach (var targetId in successors[node.BpmnId])
			{
				remainingIncoming[targetId]--;
				if (remainingIncoming[targetId] == 0)
				{
					released.Add(targetId);
				}
			}
			return released;
		}

		private static void EnqueueAll(PriorityQueue<FlowNode, FlowNode> ready, IEnumerable<FlowNode> nodes)
		{
			foreach (var node in nodes)
			{
				ready.Enqueue(node, node);
			}
		}

		private static int CompareReadyNodes(FlowNode? left, FlowNode? right)
		{
			var rankComparison = RankOf(left).CompareTo(RankOf(right));
			return rankComparison != 0 ? rankComparison : string.CompareOrdinal(left?.BpmnId, right?.BpmnId);
		}

		private static int RankOf(FlowNode? node) => node?.Kind switch
		{
			EFlowNodeKind.StartEvent => StartEventRank,
			EFlowNodeKind.EndEvent => EndEventRank,
			_ => InnerNodeRank
		};
	}
}
