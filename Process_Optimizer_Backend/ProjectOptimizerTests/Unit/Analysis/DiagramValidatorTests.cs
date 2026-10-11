using System.Numerics;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class DiagramValidatorTests
	{
		private const string ValidationFixturesFolder = "Validation";

		private readonly BpmnGraphParser _parser = new();
		private readonly DiagramValidator _validator = new(new ProcessStructureAnalyzer());

		[Fact]
		public void Validate_WorkedExample_IsOptimizableWithoutIssues()
		{
			var graph = _parser.Parse(BpmnFixtures.WorkedExample);

			var result = _validator.Validate(graph);

			Assert.Empty(result.Issues);
			Assert.True(result.IsOptimizable);
			Assert.NotNull(result.Structure);
			Assert.Equal(new BigInteger(2), result.Structure.Count);
		}

		[Fact]
		public void Validate_DefaultDiagram_ReportsOnlyDv03AndIsNotOptimizable()
		{
			var graph = _parser.Parse(DefaultDiagram.Xml);

			var result = _validator.Validate(graph);

			var issue = Assert.Single(result.Issues);
			Assert.Equal(DiagramIssueCodes.EndEventMissing, issue.Code);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.Null(issue.BpmnElementId);
			Assert.Equal("The process has no end event.", issue.Message);
			Assert.False(result.IsOptimizable);
			Assert.Null(result.Structure);
		}

		[Fact]
		public void Validate_NoStartEvent_ReportsDv02()
		{
			var builder = Tasks(new BpmnDiagramBuilder(), "A")
				.EndEvent("End")
				.Flow("F1", "A", "End");

			var result = Validate(builder);

			var issue = Assert.Single(result.Issues);
			Assert.Equal(DiagramIssueCodes.StartEventMissing, issue.Code);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.Null(issue.BpmnElementId);
			Assert.Equal("The process has no start event.", issue.Message);
			Assert.False(result.IsOptimizable);
		}

		[Fact]
		public void Validate_NoEndEvent_SkipsDeadEndCheck()
		{
			var builder = Tasks(new BpmnDiagramBuilder().StartEvent("Start"), "A")
				.Flow("F1", "Start", "A");

			var result = Validate(builder);

			Assert.Equal([(DiagramIssueCodes.EndEventMissing, (string?)null)], CodesAndIds(result));
		}

		[Fact]
		public void Validate_UnreachableNode_ReportsDv06()
		{
			var builder = Tasks(new BpmnDiagramBuilder().StartEvent("Start").EndEvent("End"), "Orphan")
				.EndEvent("OrphanEnd")
				.Flow("F1", "Start", "End")
				.Flow("F2", "Orphan", "OrphanEnd");

			var result = Validate(builder);

			Assert.Equal([(DiagramIssueCodes.Unreachable, "Orphan"), (DiagramIssueCodes.Unreachable, "OrphanEnd")], CodesAndIds(result));
			Assert.All(result.Issues, issue => Assert.Equal(EIssueSeverity.Error, issue.Severity));
			Assert.False(result.IsOptimizable);
			Assert.Null(result.Structure);
		}

		[Fact]
		public void Validate_DeadEndNode_ReportsDv07()
		{
			var builder = Tasks(new BpmnDiagramBuilder().StartEvent("Start").Gateway("Split", EFlowNodeKind.ParallelGateway), "A", "B")
				.EndEvent("End")
				.Flow("F1", "Start", "Split")
				.Flow("F2", "Split", "A")
				.Flow("F3", "Split", "B")
				.Flow("F4", "A", "End");

			var result = Validate(builder);

			var issue = Assert.Single(result.Issues);
			Assert.Equal(DiagramIssueCodes.DeadEnd, issue.Code);
			Assert.Equal("B", issue.BpmnElementId);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.False(result.IsOptimizable);
		}

		[Fact]
		public void Validate_IsolatedNode_ReportsDv06AndDv07()
		{
			var builder = Tasks(new BpmnDiagramBuilder().StartEvent("Start").EndEvent("End"), "Isolated")
				.Flow("F1", "Start", "End");

			var result = Validate(builder);

			Assert.Equal([(DiagramIssueCodes.Unreachable, "Isolated"), (DiagramIssueCodes.DeadEnd, "Isolated")], CodesAndIds(result));
		}

		[Fact]
		public void Validate_SeveralStartAndEndEvents_ReachesEveryNodeFromAnyOfThem()
		{
			var builder = Tasks(new BpmnDiagramBuilder().StartEvent("S1").StartEvent("S2"), "A", "B")
				.EndEvent("E1")
				.EndEvent("E2")
				.Flow("F1", "S1", "A")
				.Flow("F2", "A", "E1")
				.Flow("F3", "S2", "B")
				.Flow("F4", "B", "E2");

			var result = Validate(builder);

			Assert.Empty(result.Issues);
			Assert.True(result.IsOptimizable);
		}

		[Fact]
		public void Validate_Cycle_ReportsDv08ForEveryNodeOfTheCycle()
		{
			var graph = _parser.Parse(BpmnFixtures.Load(Path.Combine(ValidationFixturesFolder, "dv-08-cycle.bpmn")));

			var result = _validator.Validate(graph);

			var cycleIds = result.Issues.Where(issue => issue.Code == DiagramIssueCodes.Cycle).Select(issue => issue.BpmnElementId).ToList();
			Assert.Contains("Merge", cycleIds);
			Assert.Contains("A", cycleIds);
			Assert.Contains("Retry", cycleIds);
			Assert.DoesNotContain("Start", cycleIds);
			Assert.All(result.Issues.Where(issue => issue.Code == DiagramIssueCodes.Cycle), issue => Assert.Equal("The node is part of a cycle.", issue.Message));
			Assert.False(result.IsOptimizable);
			Assert.Null(result.Structure);
		}

		[Fact]
		public void Validate_NodeAfterCycle_IsReportedAsLeftOverOfTopologicalSort()
		{
			var graph = _parser.Parse(BpmnFixtures.Load(Path.Combine(ValidationFixturesFolder, "dv-08-cycle.bpmn")));

			var result = _validator.Validate(graph);

			Assert.Equal([(DiagramIssueCodes.Cycle, "A"), (DiagramIssueCodes.Cycle, "End"), (DiagramIssueCodes.Cycle, "Merge"), (DiagramIssueCodes.Cycle, "Retry")], CodesAndIds(result));
		}

		[Fact]
		public void Validate_TaskWithoutDuration_ReportsDv10Warning()
		{
			var builder = StartToEnd(new BpmnDiagramBuilder().Task("A", "A", null, 100), "A");

			var result = Validate(builder);

			var issue = Assert.Single(result.Issues);
			Assert.Equal(DiagramIssueCodes.MissingEstimate, issue.Code);
			Assert.Equal(EIssueSeverity.Warning, issue.Severity);
			Assert.Equal("A", issue.BpmnElementId);
			Assert.True(result.IsOptimizable);
			Assert.NotNull(result.Structure);
		}

		[Fact]
		public void Validate_TaskWithoutCost_ReportsDv10Warning()
		{
			var builder = StartToEnd(new BpmnDiagramBuilder().Task("A", "A", 3, null), "A");

			var result = Validate(builder);

			Assert.Equal([(DiagramIssueCodes.MissingEstimate, "A")], CodesAndIds(result));
		}

		[Fact]
		public void Validate_ZeroDurationAndCost_DoesNotReportDv10()
		{
			var builder = StartToEnd(new BpmnDiagramBuilder().Task("A", "A", 0, 0), "A");

			var result = Validate(builder);

			Assert.Empty(result.Issues);
		}

		[Fact]
		public void Validate_GatewayWithoutEstimates_DoesNotReportDv10()
		{
			var builder = StartToEnd(new BpmnDiagramBuilder().Gateway("G", EFlowNodeKind.ExclusiveGateway), "G");

			var result = Validate(builder);

			Assert.Empty(result.Issues);
		}

		[Fact]
		public void Validate_GatewayWithSeveralInAndOut_ReportsDv11()
		{
			var graph = _parser.Parse(BpmnFixtures.Load(Path.Combine(ValidationFixturesFolder, "dv-11-mixed-gateway.bpmn")));

			var result = _validator.Validate(graph);

			var issue = Assert.Single(result.Issues);
			Assert.Equal(DiagramIssueCodes.MixedGateway, issue.Code);
			Assert.Equal("Mixed", issue.BpmnElementId);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.False(result.IsOptimizable);
			Assert.Null(result.Structure);
		}

		[Fact]
		public void Validate_ParallelSplitClosedByExclusiveJoin_ReportsDv12()
		{
			var builder = Block(EFlowNodeKind.ParallelGateway, EFlowNodeKind.ExclusiveGateway);

			var result = Validate(builder);

			AssertSingleNotWellStructured(result, "Split");
		}

		[Fact]
		public void Validate_ExclusiveSplitClosedByParallelJoin_ReportsDv12()
		{
			var builder = Block(EFlowNodeKind.ExclusiveGateway, EFlowNodeKind.ParallelGateway);

			var result = Validate(builder);

			AssertSingleNotWellStructured(result, "Split");
		}

		[Fact]
		public void Validate_InclusiveSplitClosedByExclusiveJoin_ReportsDv12()
		{
			var builder = Block(EFlowNodeKind.InclusiveGateway, EFlowNodeKind.ExclusiveGateway);

			var result = Validate(builder);

			AssertSingleNotWellStructured(result, "Split");
		}

		[Theory]
		[InlineData(EFlowNodeKind.ParallelGateway, EFlowNodeKind.ParallelGateway)]
		[InlineData(EFlowNodeKind.ExclusiveGateway, EFlowNodeKind.ExclusiveGateway)]
		[InlineData(EFlowNodeKind.InclusiveGateway, EFlowNodeKind.InclusiveGateway)]
		[InlineData(EFlowNodeKind.EventBasedGateway, EFlowNodeKind.ExclusiveGateway)]
		public void Validate_SplitClosedByMatchingJoin_IsOptimizable(EFlowNodeKind splitKind, EFlowNodeKind joinKind)
		{
			var builder = Block(splitKind, joinKind);

			var result = Validate(builder);

			Assert.Empty(result.Issues);
			Assert.True(result.IsOptimizable);
			Assert.NotNull(result.Structure);
		}

		[Fact]
		public void Validate_ImplicitSplitAndJoin_ReportsDv13WarningAndStaysOptimizable()
		{
			var graph = _parser.Parse(BpmnFixtures.Load(Path.Combine(ValidationFixturesFolder, "dv-13-implicit-split-and-join.bpmn")));

			var result = _validator.Validate(graph);

			Assert.Equal([(DiagramIssueCodes.ImplicitSplitOrJoin, "A"), (DiagramIssueCodes.ImplicitSplitOrJoin, "D")], CodesAndIds(result));
			Assert.All(result.Issues, issue => Assert.Equal(EIssueSeverity.Warning, issue.Severity));
			Assert.True(result.IsOptimizable);
			Assert.NotNull(result.Structure);
		}

		[Fact]
		public void Validate_ImplicitSplitClosedByExclusiveJoin_ReportsDv12()
		{
			var builder = Tasks(new BpmnDiagramBuilder().StartEvent("Start"), "A", "B", "C")
				.Gateway("Join", EFlowNodeKind.ExclusiveGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "A")
				.Flow("F2", "A", "B")
				.Flow("F3", "A", "C")
				.Flow("F4", "B", "Join")
				.Flow("F5", "C", "Join")
				.Flow("F6", "Join", "End");

			var result = Validate(builder);

			Assert.Equal([(DiagramIssueCodes.NotWellStructured, "A"), (DiagramIssueCodes.ImplicitSplitOrJoin, "A")], CodesAndIds(result));
			Assert.False(result.IsOptimizable);
		}

		[Fact]
		public void Validate_ConditionOnPlainFlow_ReportsDv14Warning()
		{
			var graph = _parser.Parse(BpmnFixtures.Load(Path.Combine(ValidationFixturesFolder, "dv-14-ignored-condition.bpmn")));

			var result = _validator.Validate(graph);

			var issue = Assert.Single(result.Issues);
			Assert.Equal(DiagramIssueCodes.IgnoredCondition, issue.Code);
			Assert.Equal("F1", issue.BpmnElementId);
			Assert.Equal(EIssueSeverity.Warning, issue.Severity);
			Assert.True(result.IsOptimizable);
		}

		[Fact]
		public void Validate_ConditionOnParallelSplitFlow_ReportsDv14Warning()
		{
			var builder = Block(EFlowNodeKind.ParallelGateway, EFlowNodeKind.ParallelGateway, "budget > 0");

			var result = Validate(builder);

			Assert.Equal([(DiagramIssueCodes.IgnoredCondition, "FSplitA")], CodesAndIds(result));
		}

		[Fact]
		public void Validate_ConditionOnFlowLeavingJoin_ReportsDv14Warning()
		{
			var builder = Block(EFlowNodeKind.ExclusiveGateway, EFlowNodeKind.ExclusiveGateway, joinCondition: "done");

			var result = Validate(builder);

			Assert.Equal([(DiagramIssueCodes.IgnoredCondition, "FJoin")], CodesAndIds(result));
		}

		[Theory]
		[InlineData(EFlowNodeKind.ExclusiveGateway)]
		[InlineData(EFlowNodeKind.InclusiveGateway)]
		public void Validate_ConditionOnConditionalSplitFlow_DoesNotReportDv14(EFlowNodeKind gatewayKind)
		{
			var builder = Block(gatewayKind, gatewayKind, "budget > 0");

			var result = Validate(builder);

			Assert.Empty(result.Issues);
		}

		[Fact]
		public void Validate_EmptyConditionOnPlainFlow_ReportsDv14Warning()
		{
			var builder = Tasks(new BpmnDiagramBuilder().StartEvent("Start"), "A")
				.EndEvent("End")
				.Flow("F1", "Start", "A", string.Empty)
				.Flow("F2", "A", "End");

			var result = Validate(builder);

			Assert.Equal([(DiagramIssueCodes.IgnoredCondition, "F1")], CodesAndIds(result));
		}

		[Fact]
		public void Validate_DuplicateNames_ReportsDv15Warning()
		{
			var graph = _parser.Parse(BpmnFixtures.Load(Path.Combine(ValidationFixturesFolder, "dv-15-duplicate-name.bpmn")));

			var result = _validator.Validate(graph);

			Assert.Equal([(DiagramIssueCodes.DuplicateName, "A"), (DiagramIssueCodes.DuplicateName, "B")], CodesAndIds(result));
			Assert.All(result.Issues, issue => Assert.Equal(EIssueSeverity.Warning, issue.Severity));
			Assert.All(result.Issues, issue => Assert.Equal("Several nodes share the name 'Review'.", issue.Message));
			Assert.True(result.IsOptimizable);
		}

		[Fact]
		public void Validate_EmptyNames_AreNotDuplicates()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("A", "  ", 1, 1)
				.Task("B", "  ", 1, 1)
				.Gateway("G", EFlowNodeKind.ExclusiveGateway)
				.EndEvent("End")
				.Flow("F1", "Start", "A")
				.Flow("F2", "A", "B")
				.Flow("F3", "B", "G")
				.Flow("F4", "G", "End");

			var result = Validate(builder);

			Assert.Empty(result.Issues);
		}

		[Fact]
		public void Validate_NamesDifferingOnlyInCase_AreNotDuplicates()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("A", "Review", 1, 1)
				.Task("B", "review", 1, 1)
				.EndEvent("End")
				.Flow("F1", "Start", "A")
				.Flow("F2", "A", "B")
				.Flow("F3", "B", "End");

			var result = Validate(builder);

			Assert.Empty(result.Issues);
		}

		[Fact]
		public void Validate_InclusiveSplitWithElevenBranches_ReportsDv17()
		{
			var graph = _parser.Parse(BpmnFixtures.Load(Path.Combine(ValidationFixturesFolder, "dv-17-too-many-inclusive-branches.bpmn")));

			var result = _validator.Validate(graph);

			var issue = Assert.Single(result.Issues);
			Assert.Equal(DiagramIssueCodes.TooManyInclusiveBranches, issue.Code);
			Assert.Equal("Split", issue.BpmnElementId);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.False(result.IsOptimizable);
		}

		[Fact]
		public void Validate_InclusiveSplitWithTenBranches_IsOptimizable()
		{
			var builder = InclusiveFan(AnalysisLimits.MaxInclusiveBranches);

			var result = Validate(builder);

			Assert.Empty(result.Issues);
			Assert.True(result.IsOptimizable);
			Assert.NotNull(result.Structure);
			Assert.Equal(new BigInteger(1023), result.Structure.Count);
		}

		[Fact]
		public void Validate_ParallelSplitWithElevenBranches_DoesNotReportDv17()
		{
			var builder = Fan(EFlowNodeKind.ParallelGateway, AnalysisLimits.MaxInclusiveBranches + 1);

			var result = Validate(builder);

			Assert.Empty(result.Issues);
		}

		[Fact]
		public void Validate_WarningsOnly_IsOptimizable()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("A", "Same", null, null)
				.Task("B", "Same", 1, 1)
				.EndEvent("End")
				.Flow("F1", "Start", "A", "always")
				.Flow("F2", "A", "B")
				.Flow("F3", "B", "End");

			var result = Validate(builder);

			Assert.NotEmpty(result.Issues);
			Assert.All(result.Issues, issue => Assert.Equal(EIssueSeverity.Warning, issue.Severity));
			Assert.True(result.IsOptimizable);
			Assert.NotNull(result.Structure);
		}

		[Fact]
		public void Validate_IssuesAreSortedDeterministically()
		{
			var builder = new BpmnDiagramBuilder()
				.Task("T2", "Same", 1, null)
				.Task("T1", "Same", null, 1)
				.EndEvent("End")
				.Flow("F1", "T2", "T1")
				.Flow("F2", "T1", "End");

			var result = Validate(builder);

			Assert.Equal(
				[
					(DiagramIssueCodes.StartEventMissing, (string?)null),
					(DiagramIssueCodes.MissingEstimate, "T1"),
					(DiagramIssueCodes.MissingEstimate, "T2"),
					(DiagramIssueCodes.DuplicateName, "T1"),
					(DiagramIssueCodes.DuplicateName, "T2")
				],
				CodesAndIds(result));
		}

		[Fact]
		public void Validate_ParseIssues_AreIncluded()
		{
			var parseIssue = new ValidationIssue(DiagramIssueCodes.InvalidValue, EIssueSeverity.Error, "A", "Invalid value.");
			var graph = _parser.Parse(StartToEnd(Tasks(new BpmnDiagramBuilder(), "A"), "A").Build()) with { ParseIssues = [parseIssue] };

			var result = _validator.Validate(graph);

			Assert.Equal([parseIssue], result.Issues);
			Assert.False(result.IsOptimizable);
			Assert.Null(result.Structure);
		}

		[Fact]
		public void Validate_ErrorFoundBeforeStructureCheck_DoesNotRunStructureAnalyzer()
		{
			var analyzer = new RecordingStructureAnalyzer();
			var validator = new DiagramValidator(analyzer);
			var graph = _parser.Parse(DefaultDiagram.Xml);

			validator.Validate(graph);

			Assert.Equal(0, analyzer.CallCount);
		}

		[Fact]
		public void Validate_WarningsOnly_RunsStructureAnalyzerOnce()
		{
			var analyzer = new RecordingStructureAnalyzer();
			var validator = new DiagramValidator(analyzer);
			var graph = _parser.Parse(BpmnFixtures.Load(Path.Combine(ValidationFixturesFolder, "dv-10-missing-estimate.bpmn")));

			validator.Validate(graph);

			Assert.Equal(1, analyzer.CallCount);
		}

		[Theory]
		[InlineData("dv-01-several-processes.bpmn", DiagramIssueCodes.ProcessCount)]
		[InlineData("dv-02-no-start-event.bpmn", DiagramIssueCodes.StartEventMissing)]
		[InlineData("dv-03-no-end-event.bpmn", DiagramIssueCodes.EndEventMissing)]
		[InlineData("dv-04-unsupported-element.bpmn", DiagramIssueCodes.UnsupportedElement)]
		[InlineData("dv-05-dangling-sequence-flow.bpmn", DiagramIssueCodes.DanglingSequenceFlow)]
		[InlineData("dv-06-unreachable-node.bpmn", DiagramIssueCodes.Unreachable)]
		[InlineData("dv-07-dead-end.bpmn", DiagramIssueCodes.DeadEnd)]
		[InlineData("dv-08-cycle.bpmn", DiagramIssueCodes.Cycle)]
		[InlineData("dv-09-invalid-value.bpmn", DiagramIssueCodes.InvalidValue)]
		[InlineData("dv-10-missing-estimate.bpmn", DiagramIssueCodes.MissingEstimate)]
		[InlineData("dv-11-mixed-gateway.bpmn", DiagramIssueCodes.MixedGateway)]
		[InlineData("dv-12-not-well-structured.bpmn", DiagramIssueCodes.NotWellStructured)]
		[InlineData("dv-13-implicit-split-and-join.bpmn", DiagramIssueCodes.ImplicitSplitOrJoin)]
		[InlineData("dv-14-ignored-condition.bpmn", DiagramIssueCodes.IgnoredCondition)]
		[InlineData("dv-15-duplicate-name.bpmn", DiagramIssueCodes.DuplicateName)]
		[InlineData("dv-16-expanded-sub-process.bpmn", DiagramIssueCodes.IgnoredSubProcessContent)]
		[InlineData("dv-17-too-many-inclusive-branches.bpmn", DiagramIssueCodes.TooManyInclusiveBranches)]
		public void Validate_FixturePerDvCode_ReportsThatCode(string fixture, string expectedCode)
		{
			var graph = _parser.Parse(BpmnFixtures.Load(Path.Combine(ValidationFixturesFolder, fixture)));

			var result = _validator.Validate(graph);

			Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
		}

		private DiagramValidationResult Validate(BpmnDiagramBuilder builder) => _validator.Validate(_parser.Parse(builder.Build()));

		private static List<(string Code, string? BpmnElementId)> CodesAndIds(DiagramValidationResult result) =>
			result.Issues.Select(issue => (issue.Code, issue.BpmnElementId)).ToList();

		private static void AssertSingleNotWellStructured(DiagramValidationResult result, string expectedElementId)
		{
			var issue = Assert.Single(result.Issues);
			Assert.Equal(DiagramIssueCodes.NotWellStructured, issue.Code);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.Equal(expectedElementId, issue.BpmnElementId);
			Assert.False(result.IsOptimizable);
			Assert.Null(result.Structure);
		}

		private static BpmnDiagramBuilder Tasks(BpmnDiagramBuilder builder, params string[] ids)
		{
			foreach (var id in ids)
			{
				builder.Task(id, id, 1, 1);
			}
			return builder;
		}

		private static BpmnDiagramBuilder StartToEnd(BpmnDiagramBuilder builder, string innerNodeId) =>
			builder
				.StartEvent("Start")
				.EndEvent("End")
				.Flow("FStart", "Start", innerNodeId)
				.Flow("FEnd", innerNodeId, "End");

		private static BpmnDiagramBuilder Block(EFlowNodeKind splitKind, EFlowNodeKind joinKind, string? splitCondition = null, string? joinCondition = null)
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", splitKind);
			return Tasks(builder, "A", "B")
				.Gateway("Join", joinKind)
				.EndEvent("End")
				.Flow("FStart", "Start", "Split")
				.Flow("FSplitA", "Split", "A", splitCondition)
				.Flow("FSplitB", "Split", "B")
				.Flow("FJoinA", "A", "Join")
				.Flow("FJoinB", "B", "Join")
				.Flow("FJoin", "Join", "End", joinCondition);
		}

		private static BpmnDiagramBuilder InclusiveFan(int branchCount) => Fan(EFlowNodeKind.InclusiveGateway, branchCount);

		private static BpmnDiagramBuilder Fan(EFlowNodeKind gatewayKind, int branchCount)
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", gatewayKind)
				.Gateway("Join", gatewayKind)
				.EndEvent("End")
				.Flow("FStart", "Start", "Split")
				.Flow("FEnd", "Join", "End");
			for (var branch = 1; branch <= branchCount; branch++)
			{
				var taskId = $"T{branch:D2}";
				builder.Task(taskId, taskId, 1, 1)
					.Flow($"FS{branch:D2}", "Split", taskId)
					.Flow($"FJ{branch:D2}", taskId, "Join");
			}
			return builder;
		}

		private sealed class RecordingStructureAnalyzer : IProcessStructureAnalyzer
		{
			public int CallCount { get; private set; }

			public StructureAnalysisResult Analyze(ProcessGraph graph)
			{
				CallCount++;
				return new StructureAnalysisResult(null, []);
			}
		}
	}
}
