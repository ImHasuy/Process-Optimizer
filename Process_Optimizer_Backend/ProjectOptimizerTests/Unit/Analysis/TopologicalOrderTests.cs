using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class TopologicalOrderTests
	{
		private readonly BpmnGraphParser _parser = new();

		[Fact]
		public void Sort_WorkedExample_ReturnsStartFirstAndEndLast()
		{
			var graph = _parser.Parse(BpmnFixtures.WorkedExample);

			var ordered = TopologicalOrder.Sort(graph.Nodes, graph.Edges);

			Assert.NotNull(ordered);
			Assert.Equal(["S", "A", "G1", "B", "C", "G2", "D", "G3", "E", "F", "G4", "End"], Ids(ordered));
		}

		[Fact]
		public void Sort_ReadyNodes_AreOrderedByBpmnId()
		{
			var nodes = new[] { Node("Start", EFlowNodeKind.StartEvent), Node("Z"), Node("M"), Node("B") };
			var edges = new[] { Edge("Start", "Z"), Edge("Start", "M"), Edge("Start", "B") };

			var ordered = TopologicalOrder.Sort(nodes, edges);

			Assert.NotNull(ordered);
			Assert.Equal(["Start", "B", "M", "Z"], Ids(ordered));
		}

		[Fact]
		public void Sort_ReadyEndEvent_ComesAfterReadyInnerNodes()
		{
			var nodes = new[] { Node("Start", EFlowNodeKind.StartEvent), Node("A", EFlowNodeKind.EndEvent), Node("Z"), Node("Y", EFlowNodeKind.EndEvent) };
			var edges = new[] { Edge("Start", "A"), Edge("Start", "Z"), Edge("Z", "Y") };

			var ordered = TopologicalOrder.Sort(nodes, edges);

			Assert.NotNull(ordered);
			Assert.Equal(["Start", "Z", "A", "Y"], Ids(ordered));
		}

		[Fact]
		public void Sort_ReadyStartEvent_ComesBeforeReadyInnerNodes()
		{
			var nodes = new[] { Node("A"), Node("Z", EFlowNodeKind.StartEvent), Node("End", EFlowNodeKind.EndEvent) };
			var edges = new[] { Edge("A", "End"), Edge("Z", "End") };

			var ordered = TopologicalOrder.Sort(nodes, edges);

			Assert.NotNull(ordered);
			Assert.Equal(["Z", "A", "End"], Ids(ordered));
		}

		[Fact]
		public void Sort_CyclicGraph_ReturnsNull()
		{
			var nodes = new[] { Node("Start", EFlowNodeKind.StartEvent), Node("A"), Node("B") };
			var edges = new[] { Edge("Start", "A"), Edge("A", "B"), Edge("B", "A") };

			var ordered = TopologicalOrder.Sort(nodes, edges);

			Assert.Null(ordered);
		}

		[Fact]
		public void SortAcyclicPart_CyclicGraph_ReturnsOnlyNodesBeforeTheCycle()
		{
			var nodes = new[] { Node("Start", EFlowNodeKind.StartEvent), Node("A"), Node("B"), Node("End", EFlowNodeKind.EndEvent) };
			var edges = new[] { Edge("Start", "A"), Edge("A", "B"), Edge("B", "A"), Edge("B", "End") };

			var ordered = TopologicalOrder.SortAcyclicPart(nodes, edges);

			Assert.Equal(["Start"], Ids(ordered));
		}

		[Fact]
		public void Sort_DuplicateEdges_CountOnce()
		{
			var nodes = new[] { Node("Start", EFlowNodeKind.StartEvent), Node("A") };
			var edges = new[] { Edge("Start", "A"), Edge("Start", "A") };

			var ordered = TopologicalOrder.Sort(nodes, edges);

			Assert.NotNull(ordered);
			Assert.Equal(["Start", "A"], Ids(ordered));
		}

		[Fact]
		public void Sort_EdgeToUnknownNode_IsIgnored()
		{
			var nodes = new[] { Node("Start", EFlowNodeKind.StartEvent), Node("A") };
			var edges = new[] { Edge("Start", "A"), Edge("A", "Ghost"), Edge("Ghost", "Start") };

			var ordered = TopologicalOrder.Sort(nodes, edges);

			Assert.NotNull(ordered);
			Assert.Equal(["Start", "A"], Ids(ordered));
		}

		[Fact]
		public void Sort_EmptyGraph_ReturnsEmptyList()
		{
			var ordered = TopologicalOrder.Sort([], []);

			Assert.NotNull(ordered);
			Assert.Empty(ordered);
		}

		private static List<string> Ids(IEnumerable<FlowNode> nodes) => nodes.Select(node => node.BpmnId).ToList();

		private static FlowNode Node(string id, EFlowNodeKind kind = EFlowNodeKind.Task) =>
			new(id, id, kind, kind.ToString(), 0, 0, true, true, true, EGatewayDirection.None, true, [], [], null, []);

		private static SequenceFlowEdge Edge(string sourceId, string targetId) =>
			new($"{sourceId}-{targetId}", sourceId, targetId, string.Empty, null, false);
	}
}
