using System.Numerics;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class ScenarioIndexerTests
	{
		private const int RandomDiagramCount = 200;

		private readonly AnalysisTestComponents _components = AnalysisTestComponents.Create();
		private readonly ScenarioIndexer _indexer = new();

		public static IEnumerable<object[]> RandomSeeds => Enumerable.Range(1, RandomDiagramCount).Select(seed => new object[] { seed });

		[Fact]
		public void Unrank_WorkedExampleIndexZero_ReturnsG3EqualsE()
		{
			var root = StructureOf(BpmnFixtures.WorkedExample);

			var selection = _indexer.Unrank(root, 0);

			Assert.Equal(new BigInteger(2), root.Count);
			Assert.Equal(0, selection.Index);
			Assert.Equal("G3=E", selection.Key);
			var choice = Assert.Single(selection.Choices);
			Assert.Equal("G3", choice.GatewayId);
			Assert.Equal(EFlowNodeKind.ExclusiveGateway, choice.GatewayKind);
			Assert.True(choice.IsControllable);
			Assert.Equal(new[] { "E" }, choice.ChosenTargetIds);
		}

		[Fact]
		public void Unrank_WorkedExampleIndexOne_ReturnsG3EqualsF()
		{
			var root = StructureOf(BpmnFixtures.WorkedExample);

			var selection = _indexer.Unrank(root, 1);

			Assert.Equal(1, selection.Index);
			Assert.Equal("G3=F", selection.Key);
			Assert.Equal(new[] { "F" }, Assert.Single(selection.Choices).ChosenTargetIds);
		}

		[Fact]
		public void Unrank_InclusiveSplit_OrdersSubsetsBySizeThenLexicographically()
		{
			var root = StructureOf(BpmnFixtures.InclusiveSplitDiagram());

			var keys = UnrankAllKeys(root);

			Assert.Equal(new[] { "Or=A", "Or=B", "Or=C", "Or=A+B", "Or=A+C", "Or=B+C", "Or=A+B+C" }, keys);
		}

		[Fact]
		public void Unrank_InclusiveSplit_ReportsInclusiveGatewayKind()
		{
			var root = StructureOf(BpmnFixtures.InclusiveSplitDiagram());

			var selection = _indexer.Unrank(root, 3);

			var choice = Assert.Single(selection.Choices);
			Assert.Equal(EFlowNodeKind.InclusiveGateway, choice.GatewayKind);
			Assert.Equal(new[] { "A", "B" }, choice.ChosenTargetIds);
		}

		[Fact]
		public void Unrank_NestedExclusive_OmitsChoicesOfUnchosenBranches()
		{
			var root = StructureOf(NestedExclusiveDiagram());

			var keys = UnrankAllKeys(root);

			Assert.Equal(new[] { "X1=P", "X1=Q;X2=R", "X1=Q;X2=T" }, keys);
		}

		[Fact]
		public void Unrank_NestedExclusive_ListsOuterChoiceBeforeInnerChoice()
		{
			var root = StructureOf(NestedExclusiveDiagram());

			var selection = _indexer.Unrank(root, 2);

			Assert.Equal(new[] { "X1", "X2" }, selection.Choices.Select(choice => choice.GatewayId));
		}

		[Fact]
		public void Unrank_SequenceOfChoices_UsesLastChoiceAsLeastSignificantDigit()
		{
			var root = StructureOf(BpmnDiagramBuilder.SequentialExclusiveChoices(2));

			var keys = UnrankAllKeys(root);

			Assert.Equal(
				new[]
				{
					"Split1=Task1A;Split2=Task2A",
					"Split1=Task1A;Split2=Task2B",
					"Split1=Task1B;Split2=Task2A",
					"Split1=Task1B;Split2=Task2B"
				},
				keys);
		}

		[Fact]
		public void Unrank_EventBasedSplit_ReportsNotControllable()
		{
			var root = StructureOf(EventBasedSplitDiagram());

			var selection = _indexer.Unrank(root, 1);

			var choice = Assert.Single(selection.Choices);
			Assert.Equal(EFlowNodeKind.EventBasedGateway, choice.GatewayKind);
			Assert.False(choice.IsControllable);
			Assert.Equal("Ev=B", selection.Key);
		}

		[Fact]
		public void Unrank_DiagramWithoutChoices_ReturnsEmptyKey()
		{
			var root = StructureOf(BpmnDiagramBuilder.Chain(3));

			var selection = _indexer.Unrank(root, 0);

			Assert.Equal(BigInteger.One, root.Count);
			Assert.Equal(string.Empty, selection.Key);
			Assert.Empty(selection.Choices);
		}

		[Fact]
		public void Unrank_SameIndexTwice_ReturnsEqualSelections()
		{
			var root = StructureOf(NestedExclusiveDiagram());

			var first = _indexer.Unrank(root, 1);
			var second = _indexer.Unrank(root, 1);

			Assert.Equal(first.Key, second.Key);
			Assert.Equal(first.Choices, second.Choices, new ChoiceComparer());
		}

		[Theory]
		[InlineData("G3=E", 0L)]
		[InlineData("G3=F", 1L)]
		public void Rank_WorkedExampleKeys_ReturnsIndices(string key, long expectedIndex)
		{
			var root = StructureOf(BpmnFixtures.WorkedExample);

			var index = _indexer.Rank(root, key);

			Assert.Equal(expectedIndex, index);
		}

		[Fact]
		public void Rank_DiagramWithoutChoicesAndEmptyKey_ReturnsZero()
		{
			var root = StructureOf(BpmnDiagramBuilder.Chain(2));

			var index = _indexer.Rank(root, string.Empty);

			Assert.Equal(0L, index);
		}

		[Fact]
		public void Rank_InclusiveTargetsInAnyOrder_ReturnsSubsetIndex()
		{
			var root = StructureOf(BpmnFixtures.InclusiveSplitDiagram());

			var index = _indexer.Rank(root, "Or=C+A");

			Assert.Equal(4L, index);
		}

		[Fact]
		public void Rank_UnknownGateway_ReturnsNull()
		{
			var root = StructureOf(BpmnFixtures.WorkedExample);

			var index = _indexer.Rank(root, "G9=E");

			Assert.Null(index);
		}

		[Fact]
		public void Rank_UnknownTarget_ReturnsNull()
		{
			var root = StructureOf(BpmnFixtures.WorkedExample);

			var index = _indexer.Rank(root, "G3=X");

			Assert.Null(index);
		}

		[Fact]
		public void Rank_SurplusChoice_ReturnsNull()
		{
			var root = StructureOf(BpmnFixtures.WorkedExample);

			var index = _indexer.Rank(root, "G3=E;G1=B");

			Assert.Null(index);
		}

		[Fact]
		public void Rank_SurplusChoiceInsideUnchosenBranch_ReturnsNull()
		{
			var root = StructureOf(NestedExclusiveDiagram());

			var index = _indexer.Rank(root, "X1=P;X2=R");

			Assert.Null(index);
		}

		[Fact]
		public void Rank_MissingChoice_ReturnsNull()
		{
			var root = StructureOf(BpmnFixtures.WorkedExample);

			var index = _indexer.Rank(root, string.Empty);

			Assert.Null(index);
		}

		[Fact]
		public void Rank_MissingInnerChoice_ReturnsNull()
		{
			var root = StructureOf(NestedExclusiveDiagram());

			var index = _indexer.Rank(root, "X1=Q");

			Assert.Null(index);
		}

		[Fact]
		public void Rank_DuplicateGateway_ReturnsNull()
		{
			var root = StructureOf(BpmnFixtures.WorkedExample);

			var index = _indexer.Rank(root, "G3=E;G3=F");

			Assert.Null(index);
		}

		[Fact]
		public void Rank_TwoTargetsOnExclusiveSplit_ReturnsNull()
		{
			var root = StructureOf(BpmnFixtures.WorkedExample);

			var index = _indexer.Rank(root, "G3=E+F");

			Assert.Null(index);
		}

		[Fact]
		public void Rank_DuplicateTargetOnInclusiveSplit_ReturnsNull()
		{
			var root = StructureOf(BpmnFixtures.InclusiveSplitDiagram());

			var index = _indexer.Rank(root, "Or=A+A");

			Assert.Null(index);
		}

		[Theory]
		[InlineData("G3")]
		[InlineData("=E")]
		[InlineData("G3=")]
		[InlineData("G3=E+")]
		[InlineData("G3=E=F")]
		[InlineData("G3=E;")]
		public void Rank_MalformedKey_ReturnsNull(string key)
		{
			var root = StructureOf(BpmnFixtures.WorkedExample);

			var index = _indexer.Rank(root, key);

			Assert.Null(index);
		}

		[Fact]
		public void Rank_CountAboveIndexableLimit_ReturnsNull()
		{
			var root = new StructureFragment(EFragmentKind.Sequence, "S", null, null, null, null, true, [], new BigInteger(AnalysisLimits.MaxIndexableScenarios) + 1);

			var index = _indexer.Rank(root, string.Empty);

			Assert.Null(index);
		}

		[Theory]
		[MemberData(nameof(RandomSeeds))]
		public void RankOfUnrank_TwoHundredRandomDiagrams_ReturnsSameIndex(int seed)
		{
			var root = StructureOf(RandomDiagramGenerator.Generate(seed));

			var mismatches = Enumerable.Range(0, (int)root.Count)
				.Where(index => _indexer.Rank(root, _indexer.Unrank(root, index).Key) != index)
				.ToList();

			Assert.Empty(mismatches);
		}

		[Fact]
		public void FormatKey_InclusiveChoice_JoinsSortedTargetsWithPlus()
		{
			var choices = new[] { Choice("G5", EFlowNodeKind.InclusiveGateway, "C", "B") };

			var key = ScenarioKeyFormatter.Format(choices);

			Assert.Equal("G5=B+C", key);
		}

		[Fact]
		public void FormatKey_SeveralChoices_JoinsWithSemicolon()
		{
			var choices = new[] { Choice("G3", EFlowNodeKind.ExclusiveGateway, "E"), Choice("G5", EFlowNodeKind.InclusiveGateway, "B", "C") };

			var key = ScenarioKeyFormatter.Format(choices);

			Assert.Equal("G3=E;G5=B+C", key);
		}

		[Fact]
		public void FormatKey_NoChoices_ReturnsEmptyString()
		{
			var choices = Array.Empty<ScenarioChoice>();

			var key = ScenarioKeyFormatter.Format(choices);

			Assert.Equal(string.Empty, key);
		}

		[Fact]
		public void FormatKey_TargetsDifferingInCase_SortsOrdinally()
		{
			var choices = new[] { Choice("G", EFlowNodeKind.InclusiveGateway, "b", "B", "a") };

			var key = ScenarioKeyFormatter.Format(choices);

			Assert.Equal("G=B+a+b", key);
		}

		private StructureFragment StructureOf(string bpmnXml)
		{
			var graph = _components.Parser.Parse(bpmnXml);
			return _components.StructureAnalyzer.Analyze(graph).Root ?? throw new InvalidOperationException("The test diagram is not well-structured.");
		}

		private List<string> UnrankAllKeys(StructureFragment root) =>
			Enumerable.Range(0, (int)root.Count).Select(index => _indexer.Unrank(root, index).Key).ToList();

		private static ScenarioChoice Choice(string gatewayId, EFlowNodeKind kind, params string[] targetIds) => new(gatewayId, kind, true, targetIds);

		private static string EventBasedSplitDiagram() =>
			BranchingDiagram("Ev", EFlowNodeKind.EventBasedGateway, EFlowNodeKind.ExclusiveGateway, "A", "B");

		private static string BranchingDiagram(string splitId, EFlowNodeKind splitKind, EFlowNodeKind joinKind, params string[] taskIds)
		{
			var joinId = $"{splitId}Join";
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.EndEvent("End")
				.Gateway(splitId, splitKind)
				.Gateway(joinId, joinKind)
				.Flow("FlowStart", "Start", splitId)
				.Flow("FlowEnd", joinId, "End");
			foreach (var taskId in taskIds)
			{
				builder.Task(taskId, taskId, 1, 100).Flow($"FlowTo{taskId}", splitId, taskId).Flow($"FlowFrom{taskId}", taskId, joinId);
			}

			return builder.Build();
		}

		private static string NestedExclusiveDiagram() =>
			new BpmnDiagramBuilder()
				.StartEvent("Start")
				.EndEvent("End")
				.Gateway("X1", EFlowNodeKind.ExclusiveGateway)
				.Gateway("X1Join", EFlowNodeKind.ExclusiveGateway)
				.Gateway("X2", EFlowNodeKind.ExclusiveGateway)
				.Gateway("X2Join", EFlowNodeKind.ExclusiveGateway)
				.Task("P", "P", 1, 100)
				.Task("Q", "Q", 1, 100)
				.Task("R", "R", 1, 100)
				.Task("T", "T", 1, 100)
				.Flow("F1", "Start", "X1")
				.Flow("F2", "X1", "P")
				.Flow("F3", "P", "X1Join")
				.Flow("F4", "X1", "Q")
				.Flow("F5", "Q", "X2")
				.Flow("F6", "X2", "R")
				.Flow("F7", "X2", "T")
				.Flow("F8", "R", "X2Join")
				.Flow("F9", "T", "X2Join")
				.Flow("F10", "X2Join", "X1Join")
				.Flow("F11", "X1Join", "End")
				.Build();

		private sealed class ChoiceComparer : IEqualityComparer<ScenarioChoice>
		{
			public bool Equals(ScenarioChoice? x, ScenarioChoice? y) =>
				x is not null && y is not null && x.GatewayId == y.GatewayId && x.GatewayKind == y.GatewayKind && x.IsControllable == y.IsControllable && x.ChosenTargetIds.SequenceEqual(y.ChosenTargetIds);

			public int GetHashCode(ScenarioChoice obj) => obj.GatewayId.GetHashCode(StringComparison.Ordinal);
		}
	}
}
