using System.Numerics;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class ResourceLimitCalculatorTests
	{
		private const int RandomSeedCount = 50;
		private const int SeedSearchLimit = 5000;
		private const long MaxCrossCheckedScenarios = 256;
		private const int ChoicesAboveIndexLimit = 53;
		private const int ChoicesBelowIndexLimit = 52;

		private static readonly RevisionContext Context = new(Guid.Parse("5b0f3c52-6f1e-4d0a-9a43-2f1c7d8e9a10"), 3, ETimeUnit.Days, "EUR");

		private readonly AnalysisTestComponents _components = AnalysisTestComponents.Create();
		private readonly ResourceLimitCalculator _calculator = new();

		public static TheoryData<int> CrossCheckSeeds()
		{
			var components = AnalysisTestComponents.Create();
			var seeds = new TheoryData<int>();
			var found = 0;
			for (var seed = 1; seed <= SeedSearchLimit && found < RandomSeedCount; seed++)
			{
				var analysis = components.Analyze(RandomDiagramGenerator.Generate(seed));
				if (analysis.IsOptimizable && analysis.Structure is not null && analysis.ScenarioCount <= MaxCrossCheckedScenarios)
				{
					seeds.Add(seed);
					found++;
				}
			}

			return seeds;
		}

		[Fact]
		public void Calculate_WorkedExample_MatchesGoldenLimits()
		{
			var analysis = AnalyzeOptimizable(BpmnFixtures.WorkedExample);

			var limits = Calculate(analysis);

			Assert.Equal(25000m, limits.MaxCost);
			Assert.Equal(23000m, limits.MinCost);
			Assert.Equal(21m, limits.MaxCriticalPathDuration);
			Assert.Equal(20m, limits.MinCriticalPathDuration);
			Assert.Equal(4m, limits.MaxExclusiveDuration);
			Assert.Equal(29m, limits.MaxSequentialDuration);
		}

		[Fact]
		public void Calculate_WorkedExample_ReferencesGoldenScenarios()
		{
			var analysis = AnalyzeOptimizable(BpmnFixtures.WorkedExample);

			var limits = Calculate(analysis);

			ScenarioAssertions.AssertReference(limits.MaxCostScenario, 1, "G3=F");
			ScenarioAssertions.AssertReference(limits.MinCostScenario, 0, "G3=E");
			ScenarioAssertions.AssertReference(limits.MaxCriticalPathScenario, 0, "G3=E");
			ScenarioAssertions.AssertReference(limits.MinCriticalPathScenario, 1, "G3=F");
			ScenarioAssertions.AssertReference(limits.MaxSequentialScenario, 0, "G3=E");
		}

		[Fact]
		public void Calculate_WorkedExample_IsJointWorstCaseFalse()
		{
			var analysis = AnalyzeOptimizable(BpmnFixtures.WorkedExample);

			var limits = Calculate(analysis);

			Assert.False(limits.IsJointWorstCase);
		}

		[Fact]
		public void Calculate_WorkedExample_MaxDurationBoundIs25()
		{
			var analysis = AnalyzeOptimizable(BpmnFixtures.WorkedExample);

			var limits = Calculate(analysis);

			Assert.Equal(25m, limits.MaxDurationBound);
			Assert.True(limits.HasExclusiveActivities);
		}

		[Fact]
		public void Calculate_WorkedExample_EchoesRevisionContextAndScenarioCount()
		{
			var analysis = AnalyzeOptimizable(BpmnFixtures.WorkedExample);

			var limits = Calculate(analysis);

			Assert.Equal(Context.ProjectId, limits.ProjectId);
			Assert.Equal(Context.RevisionNumber, limits.RevisionNumber);
			Assert.Equal(ETimeUnit.Days, limits.TimeUnit);
			Assert.Equal("EUR", limits.Currency);
			Assert.Equal(EResourceLimitBasis.Planned, limits.Basis);
			Assert.Equal(2L, limits.ScenarioCount);
			Assert.False(limits.HasMissingEstimates);
			Assert.Empty(limits.Notes);
		}

		[Fact]
		public void Calculate_PessimisticBasis_UsesPessimisticEstimates()
		{
			var analysis = AnalyzeOptimizable(EstimatedChain());

			var limits = Calculate(analysis, EResourceLimitBasis.Pessimistic);

			Assert.Equal(EResourceLimitBasis.Pessimistic, limits.Basis);
			Assert.Equal(13m, limits.MaxCriticalPathDuration);
			Assert.Equal(13m, limits.MinCriticalPathDuration);
			Assert.Equal(13m, limits.MaxSequentialDuration);
			Assert.Equal(10m, limits.MaxExclusiveDuration);
			Assert.Equal(23m, limits.MaxDurationBound);
			Assert.Equal(700m, limits.MaxCost);
		}

		[Fact]
		public void Calculate_PlannedBasis_IgnoresEstimates()
		{
			var analysis = AnalyzeOptimizable(EstimatedChain());

			var limits = Calculate(analysis);

			Assert.Equal(8m, limits.MaxCriticalPathDuration);
			Assert.Equal(5m, limits.MaxExclusiveDuration);
			Assert.Equal(13m, limits.MaxDurationBound);
			Assert.Equal(700m, limits.MaxCost);
		}

		[Fact]
		public void Calculate_InclusiveSplit_MaxCostSumsAllBranches()
		{
			var analysis = AnalyzeOptimizable(InclusiveDiagram());

			var limits = Calculate(analysis);

			Assert.Equal(600m, limits.MaxCost);
			Assert.Equal(8m, limits.MaxSequentialDuration);
			ScenarioAssertions.AssertReference(limits.MaxCostScenario, 6, "Split=X+Y+Z");
			ScenarioAssertions.AssertReference(limits.MaxSequentialScenario, 6, "Split=X+Y+Z");
		}

		[Fact]
		public void Calculate_InclusiveSplit_MinCostTakesCheapestBranch()
		{
			var analysis = AnalyzeOptimizable(InclusiveDiagram());

			var limits = Calculate(analysis);

			Assert.Equal(100m, limits.MinCost);
			ScenarioAssertions.AssertReference(limits.MinCostScenario, 0, "Split=X");
		}

		[Fact]
		public void Calculate_InclusiveSplit_CriticalPathTakesSingleBranch()
		{
			var analysis = AnalyzeOptimizable(InclusiveDiagram());

			var limits = Calculate(analysis);

			Assert.Equal(5m, limits.MaxCriticalPathDuration);
			Assert.Equal(1m, limits.MinCriticalPathDuration);
			ScenarioAssertions.AssertReference(limits.MaxCriticalPathScenario, 1, "Split=Y");
			ScenarioAssertions.AssertReference(limits.MinCriticalPathScenario, 2, "Split=Z");
		}

		[Fact]
		public void Calculate_InclusiveSplit_ReferencedScenariosReachTheLimits()
		{
			var analysis = AnalyzeOptimizable(InclusiveDiagram());

			var limits = Calculate(analysis);

			AssertMatchesBruteForce(analysis, limits);
		}

		[Fact]
		public void Calculate_TiedBranches_ReferencesLowestIndex()
		{
			var analysis = AnalyzeOptimizable(TiedExclusiveDiagram());

			var limits = Calculate(analysis);

			ScenarioAssertions.AssertReference(limits.MaxCostScenario, 0, "Split=A");
			ScenarioAssertions.AssertReference(limits.MinCostScenario, 0, "Split=A");
			ScenarioAssertions.AssertReference(limits.MaxCriticalPathScenario, 0, "Split=A");
			ScenarioAssertions.AssertReference(limits.MinCriticalPathScenario, 0, "Split=A");
			ScenarioAssertions.AssertReference(limits.MaxSequentialScenario, 0, "Split=A");
			Assert.True(limits.IsJointWorstCase);
		}

		[Fact]
		public void Calculate_ParallelChoiceBlocksWithSlack_ReferencesLowestMinimumCriticalPathIndex()
		{
			var analysis = AnalyzeOptimizable(ParallelChoicesDiagram((5m, 1m), (10m, 9m)));
			var scenarios = BuildAllScenarios(analysis);

			var limits = Calculate(analysis);

			Assert.Equal(9m, limits.MinCriticalPathDuration);
			Assert.Equal(1, limits.MinCriticalPathScenario!.Index);
			Assert.Equal(LowestIndexReaching(scenarios, scenario => scenario.TotalDuration, 9m), limits.MinCriticalPathScenario.Index);
		}

		[Fact]
		public void Calculate_TiedMaximumCostWithLongerSecondBranch_IsJointWorstCase()
		{
			var analysis = AnalyzeOptimizable(SplitDiagram(EFlowNodeKind.ExclusiveGateway, ("X", 5m, 10m), ("Y", 7m, 10m)));

			var limits = Calculate(analysis);

			Assert.Equal(0, limits.MaxCostScenario!.Index);
			Assert.Equal(1, limits.MaxCriticalPathScenario!.Index);
			Assert.True(limits.IsJointWorstCase);
		}

		[Fact]
		public void Calculate_CheaperBranchIsLonger_IsNotJointWorstCase()
		{
			var analysis = AnalyzeOptimizable(SplitDiagram(EFlowNodeKind.ExclusiveGateway, ("X", 5m, 20m), ("Y", 7m, 10m)));

			var limits = Calculate(analysis);

			Assert.False(limits.IsJointWorstCase);
		}

		[Fact]
		public void Calculate_ParallelChoiceBlocks_ReferencesLowestReachingIndex()
		{
			var analysis = AnalyzeOptimizable(ParallelChoicesDiagram());
			var scenarios = BuildAllScenarios(analysis);

			var limits = Calculate(analysis);

			Assert.Equal(7m, limits.MaxCriticalPathDuration);
			Assert.Equal(2m, limits.MinCriticalPathDuration);
			Assert.Equal(LowestIndexReaching(scenarios, scenario => scenario.TotalDuration, 7m), limits.MaxCriticalPathScenario!.Index);
			Assert.Equal(LowestIndexReaching(scenarios, scenario => scenario.TotalDuration, 2m), limits.MinCriticalPathScenario!.Index);
		}

		[Fact]
		public void Calculate_ParallelExclusiveActivities_SumsExclusiveDurations()
		{
			var analysis = AnalyzeOptimizable(ParallelExclusiveTasksDiagram());

			var limits = Calculate(analysis);

			Assert.Equal(10m, limits.MaxCriticalPathDuration);
			Assert.Equal(18m, limits.MaxExclusiveDuration);
			Assert.Equal(28m, limits.MaxDurationBound);
			Assert.Equal(18m, limits.MaxSequentialDuration);
		}

		[Fact]
		public void Calculate_MissingEstimates_AddsNote()
		{
			var analysis = AnalyzeOptimizable(MissingCostChain());

			var limits = Calculate(analysis);

			Assert.True(limits.HasMissingEstimates);
			Assert.Equal(new[] { ResourceLimitCalculator.MissingEstimatesNote }, limits.Notes);
			Assert.Equal(100m, limits.MaxCost);
		}

		[Fact]
		public void Calculate_CountAboveIndexLimit_ReturnsNullCountAndReferences()
		{
			var analysis = AnalyzeOptimizable(BpmnDiagramBuilder.SequentialExclusiveChoices(ChoicesAboveIndexLimit));

			var limits = Calculate(analysis);

			Assert.True(analysis.ScenarioCount > AnalysisLimits.MaxIndexableScenarios);
			Assert.Null(limits.ScenarioCount);
			Assert.Null(limits.MaxCostScenario);
			Assert.Null(limits.MinCostScenario);
			Assert.Null(limits.MaxCriticalPathScenario);
			Assert.Null(limits.MinCriticalPathScenario);
			Assert.Null(limits.MaxSequentialScenario);
			Assert.Equal(new[] { ResourceLimitCalculator.TooManyScenariosNote }, limits.Notes);
		}

		[Fact]
		public void Calculate_CountAboveIndexLimit_StillComputesLimits()
		{
			var analysis = AnalyzeOptimizable(BpmnDiagramBuilder.SequentialExclusiveChoices(ChoicesAboveIndexLimit));

			var limits = Calculate(analysis);

			Assert.Equal(ChoicesAboveIndexLimit * 100m, limits.MaxCost);
			Assert.Equal(ChoicesAboveIndexLimit * 100m, limits.MinCost);
			Assert.Equal(TriangularNumber(ChoicesAboveIndexLimit), limits.MaxCriticalPathDuration);
			Assert.Equal(TriangularNumber(ChoicesAboveIndexLimit), limits.MaxDurationBound);
		}

		[Fact]
		public void Calculate_CountBelowIndexLimit_ReturnsCountAndReferences()
		{
			var analysis = AnalyzeOptimizable(BpmnDiagramBuilder.SequentialExclusiveChoices(ChoicesBelowIndexLimit));

			var limits = Calculate(analysis);

			Assert.Equal((long)BigInteger.Pow(2, ChoicesBelowIndexLimit), limits.ScenarioCount);
			Assert.NotNull(limits.MaxCostScenario);
			Assert.Equal(0L, limits.MaxCostScenario.Index);
			Assert.Empty(limits.Notes);
		}

		[Fact]
		public void Calculate_NoExclusiveActivities_BoundEqualsMaxCriticalPath()
		{
			var analysis = AnalyzeOptimizable(BpmnDiagramBuilder.Chain(3));

			var limits = Calculate(analysis);

			Assert.False(limits.HasExclusiveActivities);
			Assert.Equal(0m, limits.MaxExclusiveDuration);
			Assert.Equal(3m, limits.MaxCriticalPathDuration);
			Assert.Equal(limits.MaxCriticalPathDuration, limits.MaxDurationBound);
		}

		[Fact]
		public void Calculate_SingleScenario_ReferencesEmptyKeyAndJointWorstCase()
		{
			var analysis = AnalyzeOptimizable(BpmnDiagramBuilder.Chain(3));

			var limits = Calculate(analysis);

			Assert.Equal(1L, limits.ScenarioCount);
			Assert.Equal(300m, limits.MaxCost);
			ScenarioAssertions.AssertReference(limits.MaxCostScenario, 0, string.Empty);
			Assert.True(limits.IsJointWorstCase);
		}

		[Fact]
		public void Calculate_GatewayWithDuration_IsCounted()
		{
			var analysis = AnalyzeOptimizable(GatewayWithDurationDiagram());

			var limits = Calculate(analysis);

			Assert.Equal(52m, limits.MinCost);
			Assert.Equal(62m, limits.MaxCost);
			Assert.Equal(5m, limits.MaxCriticalPathDuration);
			Assert.Equal(4m, limits.MinCriticalPathDuration);
		}

		[Theory]
		[MemberData(nameof(CrossCheckSeeds))]
		public void Calculate_RandomDiagrams_ReferencedScenariosReachTheLimits(int seed)
		{
			var analysis = AnalyzeOptimizable(RandomDiagramGenerator.Generate(seed));

			var limits = Calculate(analysis);

			AssertMatchesBruteForce(analysis, limits);
		}

		[Fact]
		public void CrossCheckSeeds_FindsFiftyDiagrams()
		{
			var seeds = CrossCheckSeeds();

			var count = seeds.Count();

			Assert.Equal(RandomSeedCount, count);
		}

		private ResourceLimitsDTO Calculate(DiagramAnalysis analysis, EResourceLimitBasis basis = EResourceLimitBasis.Planned) =>
			_calculator.Calculate(analysis.Graph, analysis.Structure!, basis, Context);

		private DiagramAnalysis AnalyzeOptimizable(string xml)
		{
			var analysis = _components.Analyze(xml);
			Assert.True(analysis.IsOptimizable, string.Join("; ", analysis.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
			Assert.NotNull(analysis.Structure);
			return analysis;
		}

		private List<ScenarioComputation> BuildAllScenarios(DiagramAnalysis analysis) =>
			Enumerable.Range(0, (int)analysis.ScenarioCount)
				.Select(index => _components.Analyzer.GetScenario(analysis, index))
				.ToList();

		private void AssertMatchesBruteForce(DiagramAnalysis analysis, ResourceLimitsDTO limits)
		{
			var scenarios = BuildAllScenarios(analysis);
			Assert.Equal((long)analysis.ScenarioCount, limits.ScenarioCount);
			AssertMaximum(scenarios, limits.MaxCost, limits.MaxCostScenario, scenario => scenario.TotalCost);
			AssertMinimum(scenarios, limits.MinCost, limits.MinCostScenario, scenario => scenario.TotalCost);
			AssertMaximum(scenarios, limits.MaxCriticalPathDuration, limits.MaxCriticalPathScenario, scenario => scenario.TotalDuration);
			AssertMinimum(scenarios, limits.MinCriticalPathDuration, limits.MinCriticalPathScenario, scenario => scenario.TotalDuration);
			AssertMaximum(scenarios, limits.MaxSequentialDuration, limits.MaxSequentialScenario, scenario => scenario.SequentialDuration);
			Assert.Equal(scenarios.Max(scenario => ExclusiveDuration(analysis.Graph, scenario)), limits.MaxExclusiveDuration);
			Assert.Equal(limits.MaxCriticalPathDuration + limits.MaxExclusiveDuration, limits.MaxDurationBound);
			AssertJointWorstCaseIsReached(scenarios, limits);
		}

		private static void AssertMaximum(List<ScenarioComputation> scenarios, decimal limit, ScenarioRefDTO? reference, Func<ScenarioComputation, decimal> value)
		{
			Assert.Equal(scenarios.Max(value), limit);
			AssertReachedBy(scenarios, limit, reference, value);
		}

		private static void AssertMinimum(List<ScenarioComputation> scenarios, decimal limit, ScenarioRefDTO? reference, Func<ScenarioComputation, decimal> value)
		{
			Assert.Equal(scenarios.Min(value), limit);
			AssertReachedBy(scenarios, limit, reference, value);
		}

		private static void AssertReachedBy(List<ScenarioComputation> scenarios, decimal limit, ScenarioRefDTO? reference, Func<ScenarioComputation, decimal> value)
		{
			Assert.NotNull(reference);
			var scenario = scenarios[(int)reference.Index];
			Assert.Equal(limit, value(scenario));
			Assert.Equal(scenario.Selection.Key, reference.Key);
			Assert.Equal(LowestIndexReaching(scenarios, value, limit), reference.Index);
		}

		private static void AssertJointWorstCaseIsReached(List<ScenarioComputation> scenarios, ResourceLimitsDTO limits)
		{
			var isReached = scenarios.Any(scenario =>
				scenario.TotalCost == limits.MaxCost && scenario.TotalDuration == limits.MaxCriticalPathDuration);
			Assert.Equal(isReached, limits.IsJointWorstCase);
		}

		private static decimal ExclusiveDuration(ProcessGraph graph, ScenarioComputation scenario) =>
			scenario.ElementIds
				.Select(id => graph.NodesById[id])
				.Where(node => !node.Parallelizable)
				.Sum(node => node.Duration);

		private static long LowestIndexReaching(List<ScenarioComputation> scenarios, Func<ScenarioComputation, decimal> value, decimal limit) =>
			scenarios.First(scenario => value(scenario) == limit).Selection.Index;

		private static decimal TriangularNumber(int count) => count * (count + 1) / 2m;

		private static string EstimatedChain() =>
			new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Raw("<bpmn:task id=\"A\" name=\"Build\" po:duration=\"5\" po:cost=\"400\" po:parallelizable=\"false\"><bpmn:extensionElements><po:estimate optimistic=\"3\" mostLikely=\"5\" pessimistic=\"10\" /></bpmn:extensionElements></bpmn:task>")
				.Task("B", "Review", 3, 300)
				.EndEvent("End")
				.Flow("F1", "Start", "A")
				.Flow("F2", "A", "B")
				.Flow("F3", "B", "End")
				.Build();

		private static string InclusiveDiagram() =>
			SplitDiagram(EFlowNodeKind.InclusiveGateway, ("X", 2m, 100m), ("Y", 5m, 300m), ("Z", 1m, 200m));

		private static string TiedExclusiveDiagram() =>
			SplitDiagram(EFlowNodeKind.ExclusiveGateway, ("A", 3m, 100m), ("B", 3m, 100m));

		private static string ParallelExclusiveTasksDiagram() =>
			new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", EFlowNodeKind.ParallelGateway)
				.Task("B", "Backend", 10, 100, false)
				.Task("C", "Frontend", 8, 100, false)
				.Gateway("Join", EFlowNodeKind.ParallelGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "Split")
				.Flow("F2", "Split", "B")
				.Flow("F3", "Split", "C")
				.Flow("F4", "B", "Join")
				.Flow("F5", "C", "Join")
				.Flow("F6", "Join", "End")
				.Build();

		private static string SplitDiagram(EFlowNodeKind kind, params (string Id, decimal Duration, decimal Cost)[] branches)
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", kind)
				.Gateway("Join", kind)
				.EndEvent("End")
				.Flow("FlowStart", "Start", "Split")
				.Flow("FlowEnd", "Join", "End");
			foreach (var branch in branches)
			{
				builder.Task(branch.Id, branch.Id, branch.Duration, branch.Cost)
					.Flow($"FlowIn{branch.Id}", "Split", branch.Id)
					.Flow($"FlowOut{branch.Id}", branch.Id, "Join");
			}

			return builder.Build();
		}

		private static string ParallelChoicesDiagram() => ParallelChoicesDiagram((2m, 7m), (7m, 1m));

		private static string ParallelChoicesDiagram(params (decimal First, decimal Second)[] branchDurations)
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Fork", EFlowNodeKind.ParallelGateway)
				.Gateway("Sync", EFlowNodeKind.ParallelGateway)
				.EndEvent("End")
				.Flow("FlowStart", "Start", "Fork")
				.Flow("FlowEnd", "Sync", "End");
			for (var branch = 0; branch < branchDurations.Length; branch++)
			{
				AddChoiceBranch(builder, branch + 1, branchDurations[branch].First, branchDurations[branch].Second);
			}

			return builder.Build();
		}

		private static void AddChoiceBranch(BpmnDiagramBuilder builder, int branch, decimal firstDuration, decimal secondDuration)
		{
			var choiceId = $"Choice{branch}";
			var mergeId = $"Merge{branch}";
			builder.Gateway(choiceId, EFlowNodeKind.ExclusiveGateway)
				.Gateway(mergeId, EFlowNodeKind.ExclusiveGateway)
				.Task($"P{branch}", $"P{branch}", firstDuration, 100)
				.Task($"Q{branch}", $"Q{branch}", secondDuration, 100)
				.Flow($"FlowFork{branch}", "Fork", choiceId)
				.Flow($"FlowP{branch}", choiceId, $"P{branch}")
				.Flow($"FlowQ{branch}", choiceId, $"Q{branch}")
				.Flow($"FlowPMerge{branch}", $"P{branch}", mergeId)
				.Flow($"FlowQMerge{branch}", $"Q{branch}", mergeId)
				.Flow($"FlowSync{branch}", mergeId, "Sync");
		}

		private static string MissingCostChain() =>
			new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("A", "Priced", 2, 100)
				.Task("B", "Unpriced", 3, null)
				.EndEvent("End")
				.Flow("F1", "Start", "A")
				.Flow("F2", "A", "B")
				.Flow("F3", "B", "End")
				.Build();

		private static string GatewayWithDurationDiagram() =>
			new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Raw("<bpmn:exclusiveGateway id=\"Split\" po:duration=\"2\" po:cost=\"50\" />")
				.Task("A", "Cheap", 2, 2)
				.Task("B", "Expensive", 3, 12)
				.Gateway("Join", EFlowNodeKind.ExclusiveGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "Split")
				.Flow("F2", "Split", "A")
				.Flow("F3", "Split", "B")
				.Flow("F4", "A", "Join")
				.Flow("F5", "B", "Join")
				.Flow("F6", "Join", "End")
				.Build();
	}
}
