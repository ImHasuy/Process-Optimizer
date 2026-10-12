using System.Text.Json;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class ScenarioBuilderTests
	{
		private const string CyclicScenarioMessage = "The scenario graph contains a cycle.";

		private readonly AnalysisTestComponents _components = AnalysisTestComponents.Create();
		private readonly ScenarioBuilder _builder = new(new CriticalPathCalculator());

		[Fact]
		public void Build_WorkedExampleScenarioZero_Returns21DaysAnd23000()
		{
			var computation = BuildWorkedExample(0);

			Assert.Equal("G3=E", computation.Selection.Key);
			Assert.Equal(21m, computation.TotalDuration);
			Assert.Equal(23000m, computation.TotalCost);
		}

		[Fact]
		public void Build_WorkedExampleScenarioOne_Returns20Days25000AndSequential28()
		{
			var computation = BuildWorkedExample(1);

			Assert.Equal("G3=F", computation.Selection.Key);
			Assert.Equal(20m, computation.TotalDuration);
			Assert.Equal(25000m, computation.TotalCost);
			Assert.Equal(28m, computation.SequentialDuration);
		}

		[Fact]
		public void Build_WorkedExampleScenarioZero_SequentialDurationIs29()
		{
			var computation = BuildWorkedExample(0);

			Assert.Equal(29m, computation.SequentialDuration);
		}

		[Fact]
		public void Build_WorkedExampleScenarioOne_ActivityCHasSlackTwo()
		{
			var computation = BuildWorkedExample(1);

			var frontend = computation.Activities.Single(activity => activity.Node.BpmnId == "C");
			Assert.Equal(5m, frontend.EarliestStart);
			Assert.Equal(13m, frontend.EarliestFinish);
			Assert.Equal(7m, frontend.LatestStart);
			Assert.Equal(15m, frontend.LatestFinish);
			Assert.Equal(2m, frontend.Slack);
			Assert.False(frontend.IsCritical);
		}

		[Fact]
		public void Build_WorkedExampleScenarioOne_ListsElementIdsAndSequenceFlowIds()
		{
			var computation = BuildWorkedExample(1);

			Assert.Equal(new[] { "S", "A", "G1", "B", "C", "G2", "D", "G3", "F", "G4", "End" }, computation.ElementIds);
			Assert.Equal(new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F10", "F12", "F13" }, computation.SequenceFlowIds);
		}

		[Fact]
		public void Build_WorkedExampleScenarioOne_ReturnsCriticalPathWithGatewaysAndEvents()
		{
			var computation = BuildWorkedExample(1);

			Assert.Equal(new[] { "S", "A", "G1", "B", "G2", "D", "G3", "F", "G4", "End" }, computation.CriticalPath);
		}

		[Theory]
		[InlineData(0L)]
		[InlineData(1L)]
		public void Build_WorkedExample_PathCountIsTwo(long index)
		{
			var computation = BuildWorkedExample(index);

			Assert.Equal(2, computation.PathCount);
		}

		[Fact]
		public void Build_WorkedExample_ActivitiesExcludeEventsAndZeroGateways()
		{
			var computation = BuildWorkedExample(1);

			Assert.Equal(new[] { "A", "B", "C", "D", "F" }, computation.Activities.Select(activity => activity.Node.BpmnId));
		}

		[Fact]
		public void Build_ScenarioWithExclusiveActivity_SetsHasExclusiveActivities()
		{
			var computation = BuildWorkedExample(0);

			Assert.True(computation.HasExclusiveActivities);
			Assert.False(computation.HasMissingEstimates);
		}

		[Fact]
		public void Build_OnlyParallelizableActivities_ClearsHasExclusiveActivities()
		{
			var computation = BuildWithoutChoices(BpmnDiagramBuilder.Chain(2));

			Assert.False(computation.HasExclusiveActivities);
		}

		[Fact]
		public void Build_TaskWithoutCost_SetsHasMissingEstimates()
		{
			var xml = SingleTaskDiagram(duration: 2, cost: null);

			var computation = BuildWithoutChoices(xml);

			Assert.True(computation.HasMissingEstimates);
		}

		[Fact]
		public void Build_TaskWithoutDuration_SetsHasMissingEstimates()
		{
			var xml = SingleTaskDiagram(duration: null, cost: 100);

			var computation = BuildWithoutChoices(xml);

			Assert.True(computation.HasMissingEstimates);
		}

		[Fact]
		public void Build_TaskWithZeroDurationAndCost_IsStillReportedAsActivity()
		{
			var xml = SingleTaskDiagram(duration: 0, cost: 0);

			var computation = BuildWithoutChoices(xml);

			Assert.Equal("Work", Assert.Single(computation.Activities).Node.BpmnId);
			Assert.False(computation.HasMissingEstimates);
		}

		[Fact]
		public void Build_EventWithDuration_IsReportedAsActivity()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Raw("<bpmn:intermediateCatchEvent id=\"Wait\" po:duration=\"3\" />")
				.EndEvent("End")
				.Flow("F1", "Start", "Wait")
				.Flow("F2", "Wait", "End")
				.Build();

			var computation = BuildWithoutChoices(xml);

			Assert.Equal("Wait", Assert.Single(computation.Activities).Node.BpmnId);
			Assert.Equal(3m, computation.TotalDuration);
		}

		[Fact]
		public void Build_InclusiveSelectionOfTwoBranches_KeepsOnlyChosenBranches()
		{
			var graph = _components.Parser.Parse(BpmnFixtures.InclusiveSplitDiagram());
			var selection = Selection(graph, "Or=A+C");

			var computation = _builder.Build(graph, selection);

			Assert.Equal(new[] { "Start", "Or", "A", "C", "OrJoin", "End" }, computation.ElementIds);
			Assert.Equal(2, computation.PathCount);
			Assert.Equal(4m, computation.TotalDuration);
			Assert.Equal(400m, computation.TotalCost);
		}

		[Fact]
		public void Build_UnreachableNode_IsExcluded()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("Work", "Work", 2, 100)
				.Task("Orphan", "Orphan", 5, 500)
				.EndEvent("End")
				.Flow("F1", "Start", "Work")
				.Flow("F2", "Work", "End")
				.Flow("F3", "Orphan", "End")
				.Build();

			var computation = BuildWithoutChoices(xml);

			Assert.DoesNotContain("Orphan", computation.ElementIds);
			Assert.DoesNotContain("F3", computation.SequenceFlowIds);
			Assert.Equal(100m, computation.TotalCost);
		}

		[Fact]
		public void Build_DuplicateSequenceFlows_CountPathOnceButListBothFlows()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("Work", "Work", 2, 100)
				.EndEvent("End")
				.Flow("F1", "Start", "Work")
				.Flow("F1b", "Start", "Work")
				.Flow("F2", "Work", "End")
				.Build();

			var computation = BuildWithoutChoices(xml);

			Assert.Equal(1, computation.PathCount);
			Assert.Equal(new[] { "F1", "F1b", "F2" }, computation.SequenceFlowIds);
		}

		[Fact]
		public void Build_ThreeParallelBranches_PathCountIsThree()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.EndEvent("End")
				.Gateway("P", EFlowNodeKind.ParallelGateway)
				.Gateway("J", EFlowNodeKind.ParallelGateway)
				.Task("A", "A", 1, 1)
				.Task("B", "B", 2, 1)
				.Task("C", "C", 3, 1)
				.Flow("F0", "Start", "P")
				.Flow("Fa", "P", "A")
				.Flow("Fb", "P", "B")
				.Flow("Fc", "P", "C")
				.Flow("Ja", "A", "J")
				.Flow("Jb", "B", "J")
				.Flow("Jc", "C", "J")
				.Flow("F9", "J", "End")
				.Build();

			var computation = BuildWithoutChoices(xml);

			Assert.Equal(3, computation.PathCount);
			Assert.Equal(3m, computation.TotalDuration);
			Assert.Equal(6m, computation.SequentialDuration);
		}

		[Fact]
		public void Build_CyclicGraph_ThrowsInvalidOperation()
		{
			var graph = _components.Parser.Parse(new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("A", "A", 1, 1)
				.Task("B", "B", 1, 1)
				.EndEvent("End")
				.Flow("F1", "Start", "A")
				.Flow("F2", "A", "B")
				.Flow("F3", "B", "A")
				.Flow("F4", "B", "End")
				.Build());
			var selection = NoChoices();

			var exception = Assert.Throws<InvalidOperationException>(() => _builder.Build(graph, selection));

			Assert.Equal(CyclicScenarioMessage, exception.Message);
		}

		[Fact]
		public void Build_SameSelectionTwice_SerialisesIdentically()
		{
			var context = WorkedExampleContext();

			var first = JsonSerializer.Serialize(_builder.ToDto(BuildWorkedExample(1), context, 2));
			var second = JsonSerializer.Serialize(_builder.ToDto(BuildWorkedExample(1), context, 2));

			Assert.Equal(first, second);
		}

		[Fact]
		public void ToDto_CopiesContextFieldsAndOrderingVersion()
		{
			var context = WorkedExampleContext();
			var computation = BuildWorkedExample(1);

			var dto = _builder.ToDto(computation, context, 2);

			Assert.Equal(context.ProjectId, dto.ProjectId);
			Assert.Equal(1, dto.OrderingVersion);
			Assert.Equal(1, dto.Index);
			Assert.Equal(2, dto.Count);
			Assert.Equal("G3=F", dto.Key);
			Assert.Equal(ETimeUnit.Days, dto.TimeUnit);
			Assert.Equal("EUR", dto.Currency);
			Assert.Equal(20m, dto.TotalDuration);
			Assert.Equal(28m, dto.SequentialDuration);
			Assert.Equal(25000m, dto.TotalCost);
			Assert.Equal(2, dto.PathCount);
			Assert.True(dto.HasExclusiveActivities);
			Assert.False(dto.HasMissingEstimates);
		}

		[Fact]
		public void ToDto_MapsChoicesAndActivities()
		{
			var computation = BuildWorkedExample(1);

			var dto = _builder.ToDto(computation, WorkedExampleContext(), 2);

			var choice = Assert.Single(dto.Choices);
			Assert.Equal("G3", choice.GatewayId);
			Assert.Equal(EFlowNodeKind.ExclusiveGateway, choice.GatewayKind);
			Assert.True(choice.IsControllable);
			Assert.Equal(new[] { "F" }, choice.ChosenTargetIds);
			var design = dto.Activities[0];
			Assert.Equal("A", design.BpmnId);
			Assert.Equal("Design", design.Name);
			Assert.Equal(EFlowNodeKind.Task, design.Kind);
			Assert.Equal(5m, design.Duration);
			Assert.Equal(4000m, design.Cost);
			Assert.True(design.Parallelizable);
			Assert.True(design.IsCritical);
			Assert.Equal(computation.ElementIds, dto.ElementIds);
			Assert.Equal(computation.CriticalPath, dto.CriticalPath);
		}

		[Fact]
		public void ToListItem_CountsActivities()
		{
			var computation = BuildWorkedExample(1);

			var item = _builder.ToListItem(computation);

			Assert.Equal(1, item.Index);
			Assert.Equal("G3=F", item.Key);
			Assert.Equal(5, item.ActivityCount);
			Assert.Equal(20m, item.TotalDuration);
			Assert.Equal(25000m, item.TotalCost);
			Assert.Equal("G3", Assert.Single(item.Choices).GatewayId);
		}


		private ScenarioComputation BuildWithoutChoices(string bpmnXml) => _builder.Build(_components.Parser.Parse(bpmnXml), NoChoices());

		private ScenarioComputation BuildWorkedExample(long index)
		{
			var graph = _components.Parser.Parse(BpmnFixtures.WorkedExample);
			var root = StructureOf(graph);
			return _builder.Build(graph, _components.Indexer.Unrank(root, index));
		}

		private ScenarioSelection Selection(ProcessGraph graph, string key)
		{
			var root = StructureOf(graph);
			var index = _components.Indexer.Rank(root, key) ?? throw new InvalidOperationException("The test key does not resolve.");
			return _components.Indexer.Unrank(root, index);
		}

		private StructureFragment StructureOf(ProcessGraph graph) =>
			_components.StructureAnalyzer.Analyze(graph).Root ?? throw new InvalidOperationException("The test diagram is not well-structured.");

		private static ScenarioSelection NoChoices() => new(0, string.Empty, []);

		private static ProjectContext WorkedExampleContext() => new(Guid.Parse("6d0c1a5e-0000-4000-8000-000000000001"), ETimeUnit.Days, "EUR");

		private static string SingleTaskDiagram(decimal? duration, decimal? cost) =>
			new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("Work", "Work", duration, cost)
				.EndEvent("End")
				.Flow("F1", "Start", "Work")
				.Flow("F2", "Work", "End")
				.Build();
	}
}
