using System.Globalization;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class CriticalPathCalculatorTests
	{
		private readonly CriticalPathCalculator _calculator = new();

		[Fact]
		public void Calculate_ChainOfTasks_MakespanIsSumOfDurations()
		{
			var nodes = new[] { Start("S"), Activity("T1", 3), Activity("T2", 4.5m), End("E") };
			var edges = new[] { Edge("F1", "S", "T1"), Edge("F2", "T1", "T2"), Edge("F3", "T2", "E") };

			var result = _calculator.Calculate(nodes, edges);

			Assert.Equal(7.5m, result.Makespan);
			Assert.Equal(new[] { "S", "T1", "T2", "E" }, result.CriticalPath);
			Assert.All(result.Schedule, scheduled => Assert.True(scheduled.IsCritical));
		}

		[Fact]
		public void Calculate_ChainOfTasks_ComputesEarliestAndLatestTimes()
		{
			var nodes = new[] { Start("S"), Activity("T1", 3), Activity("T2", 4), End("E") };
			var edges = new[] { Edge("F1", "S", "T1"), Edge("F2", "T1", "T2"), Edge("F3", "T2", "E") };

			var result = _calculator.Calculate(nodes, edges);

			var second = ScheduleOf(result, "T2");
			Assert.Equal(3m, second.EarliestStart);
			Assert.Equal(7m, second.EarliestFinish);
			Assert.Equal(3m, second.LatestStart);
			Assert.Equal(7m, second.LatestFinish);
			Assert.Equal(0m, second.Slack);
		}

		[Fact]
		public void Calculate_ParallelBranches_MakespanIsLongestBranch()
		{
			var (nodes, edges) = ParallelNetwork();

			var result = _calculator.Calculate(nodes, edges);

			Assert.Equal(5m, result.Makespan);
			Assert.Equal(new[] { "S", "P", "Long", "J", "E" }, result.CriticalPath);
		}

		[Fact]
		public void Calculate_ParallelBranches_ShortBranchHasSlack()
		{
			var (nodes, edges) = ParallelNetwork();

			var result = _calculator.Calculate(nodes, edges);

			var shortBranch = ScheduleOf(result, "Short");
			Assert.Equal(0m, shortBranch.EarliestStart);
			Assert.Equal(2m, shortBranch.EarliestFinish);
			Assert.Equal(3m, shortBranch.LatestStart);
			Assert.Equal(5m, shortBranch.LatestFinish);
			Assert.Equal(3m, shortBranch.Slack);
			Assert.False(shortBranch.IsCritical);
		}

		[Fact]
		public void Calculate_SeveralEndEvents_UsesLargestFinish()
		{
			var nodes = new[] { Start("S"), Activity("A", 3), Activity("B", 6), End("E1"), End("E2") };
			var edges = new[] { Edge("F1", "S", "A"), Edge("F2", "S", "B"), Edge("F3", "A", "E1"), Edge("F4", "B", "E2") };

			var result = _calculator.Calculate(nodes, edges);

			Assert.Equal(6m, result.Makespan);
			Assert.Equal(new[] { "S", "B", "E2" }, result.CriticalPath);
			Assert.Equal(3m, ScheduleOf(result, "A").Slack);
			Assert.Equal(3m, ScheduleOf(result, "E1").Slack);
		}

		[Fact]
		public void Calculate_TiedBranches_PicksFirstInTopologicalOrder()
		{
			var nodes = new[] { Start("S"), Activity("A", 4), Activity("B", 4), End("E") };
			var edges = new[] { Edge("F1", "S", "A"), Edge("F2", "S", "B"), Edge("F3", "A", "E"), Edge("F4", "B", "E") };

			var result = _calculator.Calculate(nodes, edges);

			Assert.Equal(new[] { "S", "A", "E" }, result.CriticalPath);
			Assert.True(ScheduleOf(result, "B").IsCritical);
		}

		[Fact]
		public void Calculate_DuplicateEdges_CountsLinkOnce()
		{
			var nodes = new[] { Start("S"), Activity("A", 2), End("E") };
			var edges = new[] { Edge("F1", "S", "A"), Edge("F1b", "S", "A"), Edge("F2", "A", "E") };

			var result = _calculator.Calculate(nodes, edges);

			Assert.Equal(2m, result.Makespan);
			Assert.Equal(new[] { "S", "A", "E" }, result.CriticalPath);
		}

		[Fact]
		public void Calculate_EdgeToNodeOutsideList_IsIgnored()
		{
			var nodes = new[] { Start("S"), Activity("A", 2), End("E") };
			var edges = new[] { Edge("F1", "S", "A"), Edge("F2", "A", "E"), Edge("F3", "A", "Missing") };

			var result = _calculator.Calculate(nodes, edges);

			Assert.Equal(2m, result.Makespan);
			Assert.Equal(3, result.Schedule.Count);
		}

		[Fact]
		public void Calculate_NoNodes_ReturnsZeroMakespanAndEmptyPath()
		{
			var nodes = Array.Empty<FlowNode>();

			var result = _calculator.Calculate(nodes, []);

			Assert.Equal(0m, result.Makespan);
			Assert.Empty(result.CriticalPath);
			Assert.Empty(result.Schedule);
		}

		[Fact]
		public void Calculate_KeepsScheduleInGivenOrder()
		{
			var (nodes, edges) = ParallelNetwork();

			var result = _calculator.Calculate(nodes, edges);

			Assert.Equal(nodes.Select(node => node.BpmnId), result.Schedule.Select(scheduled => scheduled.Node.BpmnId));
		}

		[Fact]
		public void Calculate_TrailingZeroDecimals_AreNormalised()
		{
			var nodes = new[] { Start("S"), Activity("A", 2.50m), End("E") };
			var edges = new[] { Edge("F1", "S", "A"), Edge("F2", "A", "E") };

			var result = _calculator.Calculate(nodes, edges);

			Assert.Equal("2.5", result.Makespan.ToString(CultureInfo.InvariantCulture));
		}

		[Fact]
		public void Calculate_WorkedExampleScenarioOne_CriticalPathIncludesGatewaysAndEvents()
		{
			var (nodes, edges) = WorkedExampleWithout("E");

			var result = _calculator.Calculate(nodes, edges);

			Assert.Equal(20m, result.Makespan);
			Assert.Equal(new[] { "S", "A", "G1", "B", "G2", "D", "G3", "F", "G4", "End" }, result.CriticalPath);
		}

		[Fact]
		public void Calculate_WorkedExampleScenarioZero_MakespanIs21()
		{
			var (nodes, edges) = WorkedExampleWithout("F");

			var result = _calculator.Calculate(nodes, edges);

			Assert.Equal(21m, result.Makespan);
			Assert.Equal(new[] { "S", "A", "G1", "B", "G2", "D", "G3", "E", "G4", "End" }, result.CriticalPath);
		}

		private static (IReadOnlyList<FlowNode> Nodes, IReadOnlyList<SequenceFlowEdge> Edges) WorkedExampleWithout(string droppedNodeId)
		{
			var graph = AnalysisTestComponents.Create().Parser.Parse(BpmnFixtures.WorkedExample);
			var nodes = graph.Nodes.Where(node => node.BpmnId != droppedNodeId).ToList();
			var edges = graph.Edges.Where(edge => edge.SourceId != droppedNodeId && edge.TargetId != droppedNodeId).ToList();
			return (TopologicalOrder.Sort(nodes, edges)!, edges);
		}

		private static (FlowNode[] Nodes, SequenceFlowEdge[] Edges) ParallelNetwork() =>
			(
				new[] { Start("S"), Gateway("P"), Activity("Long", 5), Activity("Short", 2), Gateway("J"), End("E") },
				new[]
				{
					Edge("F1", "S", "P"),
					Edge("F2", "P", "Long"),
					Edge("F3", "P", "Short"),
					Edge("F4", "Long", "J"),
					Edge("F5", "Short", "J"),
					Edge("F6", "J", "E")
				});

		private static ScheduledNode ScheduleOf(CriticalPathResult result, string bpmnId) =>
			result.Schedule.Single(scheduled => scheduled.Node.BpmnId == bpmnId);

		private static FlowNode Start(string id) => Node(id, EFlowNodeKind.StartEvent, 0m);

		private static FlowNode End(string id) => Node(id, EFlowNodeKind.EndEvent, 0m);

		private static FlowNode Gateway(string id) => Node(id, EFlowNodeKind.ParallelGateway, 0m);

		private static FlowNode Activity(string id, decimal duration) => Node(id, EFlowNodeKind.Task, duration);

		private static FlowNode Node(string id, EFlowNodeKind kind, decimal duration) =>
			new(id, id, kind, kind.ToString(), duration, 0m, true, true, true, EGatewayDirection.None, true, [], [], null, []);

		private static SequenceFlowEdge Edge(string id, string sourceId, string targetId) => new(id, sourceId, targetId, string.Empty, null, false);
	}
}
