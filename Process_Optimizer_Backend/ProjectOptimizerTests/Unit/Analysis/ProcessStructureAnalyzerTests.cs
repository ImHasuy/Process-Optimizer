using System.Numerics;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class ProcessStructureAnalyzerTests
	{
		private readonly BpmnGraphParser _parser = new();
		private readonly ProcessStructureAnalyzer _analyzer = new();

		[Fact]
		public void Analyze_WorkedExample_BuildsSequenceWithParallelAndExclusiveBlocks()
		{
			var graph = _parser.Parse(BpmnFixtures.WorkedExample);

			var result = _analyzer.Analyze(graph);

			Assert.Empty(result.Issues);
			var root = Assert.IsType<StructureFragment>(result.Root);
			Assert.Equal(EFragmentKind.Sequence, root.Kind);
			Assert.Equal("S", root.EntryNodeId);
			Assert.Equal(
				[EFragmentKind.Leaf, EFragmentKind.Leaf, EFragmentKind.Leaf, EFragmentKind.Parallel, EFragmentKind.Leaf, EFragmentKind.Leaf, EFragmentKind.Leaf, EFragmentKind.Exclusive, EFragmentKind.Leaf, EFragmentKind.Leaf],
				root.Children.Select(child => child.Kind));
			Assert.Equal(["S", "A", "G1", "G2", "D", "G3", "G4", "End"], LeafIds(root.Children));
		}

		[Fact]
		public void Analyze_WorkedExample_DescribesBlocksBySplitAndJoin()
		{
			var graph = _parser.Parse(BpmnFixtures.WorkedExample);

			var root = AnalyzeToRoot(graph);

			var parallel = root.Children[3];
			Assert.Equal("G1", parallel.SplitNodeId);
			Assert.Equal("G2", parallel.JoinNodeId);
			Assert.Equal("G1", parallel.EntryNodeId);
			Assert.Null(parallel.GatewayKind);
			Assert.Equal(["B", "C"], parallel.Children.Select(branch => branch.EntryNodeId));
			var exclusive = root.Children[7];
			Assert.Equal("G3", exclusive.SplitNodeId);
			Assert.Equal("G4", exclusive.JoinNodeId);
			Assert.Equal(EFlowNodeKind.ExclusiveGateway, exclusive.GatewayKind);
			Assert.True(exclusive.IsControllable);
			Assert.Equal(["E", "F"], exclusive.Children.Select(branch => branch.EntryNodeId));
		}

		[Fact]
		public void Analyze_WorkedExample_CountsTwoScenarios()
		{
			var graph = _parser.Parse(BpmnFixtures.WorkedExample);

			var root = AnalyzeToRoot(graph);

			Assert.Equal(new BigInteger(2), root.Count);
			Assert.Equal(BigInteger.One, root.Children[3].Count);
			Assert.Equal(new BigInteger(2), root.Children[7].Count);
		}

		[Fact]
		public void Analyze_EventBasedSplitClosedByExclusiveJoin_IsWellStructured()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", EFlowNodeKind.EventBasedGateway)
				.Node("intermediateCatchEvent", "Timer")
				.Node("intermediateCatchEvent", "Message")
				.Gateway("Join", EFlowNodeKind.ExclusiveGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "Split")
				.Flow("F2", "Split", "Timer")
				.Flow("F3", "Split", "Message")
				.Flow("F4", "Timer", "Join")
				.Flow("F5", "Message", "Join")
				.Flow("F6", "Join", "End");

			var root = AnalyzeToRoot(builder);

			var block = Assert.Single(root.Children, child => child.Kind == EFragmentKind.Exclusive);
			Assert.Equal(EFlowNodeKind.EventBasedGateway, block.GatewayKind);
			Assert.False(block.IsControllable);
			Assert.False(Assert.Single(root.Children, child => child.NodeId == "Split").IsControllable);
			Assert.Equal(new BigInteger(2), root.Count);
		}

		[Fact]
		public void Analyze_EmptyBranch_UsesJoinAsEntryNode()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", EFlowNodeKind.ExclusiveGateway)
				.Task("A", "A", 1, 1)
				.Gateway("Join", EFlowNodeKind.ExclusiveGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "Split")
				.Flow("F2", "Split", "A")
				.Flow("F3", "Split", "Join")
				.Flow("F4", "A", "Join")
				.Flow("F5", "Join", "End");

			var root = AnalyzeToRoot(builder);

			var block = Assert.Single(root.Children, child => child.Kind == EFragmentKind.Exclusive);
			var emptyBranch = block.Children[1];
			Assert.Equal(EFragmentKind.Sequence, emptyBranch.Kind);
			Assert.Equal("Join", emptyBranch.EntryNodeId);
			Assert.Empty(emptyBranch.Children);
			Assert.Equal(BigInteger.One, emptyBranch.Count);
			Assert.Equal(new BigInteger(2), block.Count);
		}

		[Fact]
		public void Analyze_SeveralStartEvents_WrapsThemInVirtualParallelBlock()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("S2")
				.StartEvent("S1")
				.Task("A", "A", 1, 1)
				.Task("B", "B", 1, 1)
				.Gateway("Join", EFlowNodeKind.ParallelGateway)
				.EndEvent("End")
				.Flow("F1", "S1", "A")
				.Flow("F2", "S2", "B")
				.Flow("F3", "A", "Join")
				.Flow("F4", "B", "Join")
				.Flow("F5", "Join", "End");

			var root = AnalyzeToRoot(builder);

			Assert.Equal([EFragmentKind.Parallel, EFragmentKind.Leaf, EFragmentKind.Leaf], root.Children.Select(child => child.Kind));
			var block = root.Children[0];
			Assert.Null(block.SplitNodeId);
			Assert.Equal("Join", block.JoinNodeId);
			Assert.Equal("S1", block.EntryNodeId);
			Assert.Equal("S1", root.EntryNodeId);
			Assert.Equal(["S1", "S2"], block.Children.Select(branch => branch.EntryNodeId));
		}

		[Fact]
		public void Analyze_SeveralEndEvents_WrapsThemInVirtualParallelBlock()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", EFlowNodeKind.ParallelGateway)
				.EndEvent("E2")
				.EndEvent("E1")
				.Flow("F1", "Start", "Split")
				.Flow("F2", "Split", "E2")
				.Flow("F3", "Split", "E1");

			var root = AnalyzeToRoot(builder);

			var block = root.Children[^1];
			Assert.Equal(EFragmentKind.Parallel, block.Kind);
			Assert.Equal("Split", block.SplitNodeId);
			Assert.Null(block.JoinNodeId);
			Assert.Equal(["E1", "E2"], block.Children.Select(branch => branch.EntryNodeId));
			Assert.Equal(BigInteger.One, root.Count);
		}

		[Fact]
		public void Analyze_NestedParallelSplitsEndingInDifferentEndEvents_IsWellStructured()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Outer", EFlowNodeKind.ParallelGateway)
				.Gateway("Inner", EFlowNodeKind.ParallelGateway)
				.EndEvent("E1")
				.EndEvent("E2")
				.EndEvent("E3")
				.Flow("F1", "Start", "Outer")
				.Flow("F2", "Outer", "E1")
				.Flow("F3", "Outer", "Inner")
				.Flow("F4", "Inner", "E2")
				.Flow("F5", "Inner", "E3");

			var result = _analyzer.Analyze(_parser.Parse(builder.Build()));

			Assert.Empty(result.Issues);
			Assert.NotNull(result.Root);
		}

		[Fact]
		public void Analyze_ExclusiveSplitEndingInDifferentEndEvents_ReportsDv12()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", EFlowNodeKind.ExclusiveGateway)
				.EndEvent("E1")
				.EndEvent("E2")
				.Flow("F1", "Start", "Split")
				.Flow("F2", "Split", "E1")
				.Flow("F3", "Split", "E2");

			var result = _analyzer.Analyze(_parser.Parse(builder.Build()));

			AssertNotWellStructured(result, "Split");
		}

		[Fact]
		public void Analyze_InclusiveSplitWithThreeBranches_CountsSevenScenarios()
		{
			var builder = Fan(EFlowNodeKind.InclusiveGateway, "A", "B", "C");

			var root = AnalyzeToRoot(builder);

			var block = Assert.Single(root.Children, child => child.Kind == EFragmentKind.Inclusive);
			Assert.Equal(EFlowNodeKind.InclusiveGateway, block.GatewayKind);
			Assert.Equal(new BigInteger(7), block.Count);
			Assert.Equal(new BigInteger(7), root.Count);
		}

		[Fact]
		public void Analyze_NestedBlocks_MultipliesCounts()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("P1", EFlowNodeKind.ParallelGateway)
				.Gateway("X1", EFlowNodeKind.ExclusiveGateway)
				.Gateway("J1", EFlowNodeKind.ExclusiveGateway)
				.Gateway("X2", EFlowNodeKind.ExclusiveGateway)
				.Gateway("J2", EFlowNodeKind.ExclusiveGateway)
				.Gateway("P2", EFlowNodeKind.ParallelGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "P1")
				.Flow("F2", "P1", "X1")
				.Flow("F3", "P1", "X2")
				.Flow("F4", "J1", "P2")
				.Flow("F5", "J2", "P2")
				.Flow("F6", "P2", "End");
			AddChoices(builder, "X1", "J1", "A", "B");
			AddChoices(builder, "X2", "J2", "C", "D", "E");

			var root = AnalyzeToRoot(builder);

			var parallel = Assert.Single(root.Children, child => child.Kind == EFragmentKind.Parallel);
			Assert.Equal([new BigInteger(2), new BigInteger(3)], parallel.Children.Select(branch => branch.Count));
			Assert.Equal(new BigInteger(6), root.Count);
		}

		[Fact]
		public void Analyze_BranchesOrderedByEntryNodeId()
		{
			var builder = Fan(EFlowNodeKind.ParallelGateway, "Z", "M", "B");

			var root = AnalyzeToRoot(builder);

			var block = Assert.Single(root.Children, child => child.Kind == EFragmentKind.Parallel);
			Assert.Equal(["B", "M", "Z"], block.Children.Select(branch => branch.EntryNodeId));
		}

		[Fact]
		public void Analyze_ImplicitJoinThatIsAlsoImplicitSplit_AppearsOnce()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", EFlowNodeKind.ParallelGateway)
				.Task("A", "A", 1, 1)
				.Task("B", "B", 1, 1)
				.Task("C", "C", 1, 1)
				.Task("D", "D", 1, 1)
				.Task("F", "F", 1, 1)
				.Gateway("Join", EFlowNodeKind.ParallelGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "Split")
				.Flow("F2", "Split", "A")
				.Flow("F3", "Split", "B")
				.Flow("F4", "A", "C")
				.Flow("F5", "B", "C")
				.Flow("F6", "C", "D")
				.Flow("F7", "C", "F")
				.Flow("F8", "D", "Join")
				.Flow("F9", "F", "Join")
				.Flow("F10", "Join", "End");

			var root = AnalyzeToRoot(builder);

			Assert.Single(root.Children, child => child.NodeId == "C");
			var implicitBlock = Assert.Single(root.Children, child => child.SplitNodeId == "C");
			Assert.Equal(EFragmentKind.Parallel, implicitBlock.Kind);
			Assert.Equal("Join", implicitBlock.JoinNodeId);
		}

		[Fact]
		public void Analyze_PassThroughGateway_IsLeaf()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("G", EFlowNodeKind.ExclusiveGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "G")
				.Flow("F2", "G", "End");

			var root = AnalyzeToRoot(builder);

			Assert.All(root.Children, child => Assert.Equal(EFragmentKind.Leaf, child.Kind));
			Assert.Equal(["Start", "G", "End"], LeafIds(root.Children));
			Assert.Equal(BigInteger.One, root.Count);
		}

		[Fact]
		public void Analyze_SeventyChoices_CountsBeyondLongRange()
		{
			var graph = _parser.Parse(BpmnDiagramBuilder.SequentialExclusiveChoices(70));

			var root = AnalyzeToRoot(graph);

			Assert.Equal(BigInteger.Pow(2, 70), root.Count);
		}

		[Fact]
		public void Analyze_SplitWithoutMatchingJoin_ReportsDv12()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", EFlowNodeKind.ParallelGateway)
				.Task("A", "A", 1, 1)
				.Task("B", "B", 1, 1)
				.Task("C", "C", 1, 1)
				.Gateway("J1", EFlowNodeKind.ParallelGateway)
				.Gateway("J2", EFlowNodeKind.ParallelGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "Split")
				.Flow("F2", "Split", "A")
				.Flow("F3", "Split", "B")
				.Flow("F4", "Split", "C")
				.Flow("F5", "A", "J1")
				.Flow("F6", "B", "J1")
				.Flow("F7", "J1", "J2")
				.Flow("F8", "C", "J2")
				.Flow("F9", "J2", "End");

			var result = _analyzer.Analyze(_parser.Parse(builder.Build()));

			AssertNotWellStructured(result, "Split");
		}

		[Fact]
		public void Analyze_BranchEnteredFromOutside_ReportsDv12()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", EFlowNodeKind.ParallelGateway)
				.Task("A", "A", 1, 1)
				.Task("B", "B", 1, 1)
				.Gateway("Join", EFlowNodeKind.ParallelGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "Split")
				.Flow("F2", "Split", "A")
				.Flow("F3", "Split", "B")
				.Flow("F4", "A", "B")
				.Flow("F5", "A", "Join")
				.Flow("F6", "B", "Join")
				.Flow("F7", "Join", "End");

			var result = _analyzer.Analyze(_parser.Parse(builder.Build()));

			Assert.Null(result.Root);
			var issue = Assert.Single(result.Issues);
			Assert.Equal(DiagramIssueCodes.NotWellStructured, issue.Code);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.NotNull(issue.BpmnElementId);
		}

		[Fact]
		public void Analyze_CyclicGraph_ReportsDv12WithoutElementId()
		{
			var graph = _parser.Parse(BpmnFixtures.Load(Path.Combine("Validation", "dv-08-cycle.bpmn")));

			var result = _analyzer.Analyze(graph);

			AssertNotWellStructured(result, null);
		}

		private StructureFragment AnalyzeToRoot(BpmnDiagramBuilder builder) => AnalyzeToRoot(_parser.Parse(builder.Build()));

		private StructureFragment AnalyzeToRoot(ProcessGraph graph)
		{
			var result = _analyzer.Analyze(graph);
			Assert.Empty(result.Issues);
			return Assert.IsType<StructureFragment>(result.Root);
		}

		private static List<string?> LeafIds(IEnumerable<StructureFragment> fragments) =>
			fragments.Where(fragment => fragment.Kind == EFragmentKind.Leaf).Select(fragment => fragment.NodeId).ToList();

		private static void AssertNotWellStructured(StructureAnalysisResult result, string? expectedElementId)
		{
			Assert.Null(result.Root);
			var issue = Assert.Single(result.Issues);
			Assert.Equal(DiagramIssueCodes.NotWellStructured, issue.Code);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.Equal(expectedElementId, issue.BpmnElementId);
		}

		private static BpmnDiagramBuilder Fan(EFlowNodeKind gatewayKind, params string[] taskIds)
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", gatewayKind)
				.Gateway("Join", gatewayKind)
				.EndEvent("End")
				.Flow("FStart", "Start", "Split")
				.Flow("FEnd", "Join", "End");
			return AddChoices(builder, "Split", "Join", taskIds);
		}

		private static BpmnDiagramBuilder AddChoices(BpmnDiagramBuilder builder, string splitId, string joinId, params string[] taskIds)
		{
			foreach (var taskId in taskIds)
			{
				builder.Task(taskId, taskId, 1, 1)
					.Flow($"FS{taskId}", splitId, taskId)
					.Flow($"FJ{taskId}", taskId, joinId);
			}
			return builder;
		}
	}
}
