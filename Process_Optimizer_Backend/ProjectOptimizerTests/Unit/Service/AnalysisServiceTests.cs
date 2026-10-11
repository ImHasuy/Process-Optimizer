using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Service;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Service
{
	public sealed class AnalysisServiceTests : ServiceTestBase
	{
		private const string DanglingFlowId = "FlowDangling";
		private const string UnestimatedTaskId = "Unestimated";

		private readonly AnalysisService _service;

		public AnalysisServiceTests()
		{
			_service = new AnalysisService(ScenarioCatalog);
		}

		[Fact]
		public async Task GetGraphAsync_WorkedExample_ReturnsNodesWithDependencies()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);

			var graph = await _service.GetGraphAsync(project.Id, null);

			Assert.Equal(1, graph.RevisionNumber);
			Assert.Equal(BpmnDiagramBuilder.ProcessId, graph.ProcessId);
			Assert.Equal(12, graph.Nodes.Count);
			Assert.Equal(13, graph.Edges.Count);
			var design = graph.Nodes.Single(node => node.BpmnId == "A");
			Assert.Equal("Design", design.Name);
			Assert.Equal(EFlowNodeKind.Task, design.Kind);
			Assert.Equal(5m, design.Duration);
			Assert.Equal(4000m, design.Cost);
			Assert.Equal(new[] { "B", "C" }, graph.Nodes.Single(node => node.BpmnId == "G2").DependsOn);
			Assert.Equal(new[] { "G3" }, graph.Nodes.Single(node => node.BpmnId == "D").Dependents);
			Assert.False(graph.Nodes.Single(node => node.BpmnId == "D").Parallelizable);
		}

		[Fact]
		public async Task GetGraphAsync_WorkedExample_MapsEdgeEndpoints()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);

			var graph = await _service.GetGraphAsync(project.Id, null);

			var edge = graph.Edges.Single(flow => flow.BpmnId == "F3");
			Assert.Equal("G1", edge.SourceId);
			Assert.Equal("B", edge.TargetId);
		}

		[Fact]
		public async Task GetGraphAsync_DiagramWithErrors_StillReturnsGraph()
		{
			var project = await TestData.AddProjectAsync();

			var graph = await _service.GetGraphAsync(project.Id, null);

			var node = Assert.Single(graph.Nodes);
			Assert.Equal("StartEvent_1", node.BpmnId);
			Assert.Equal(EFlowNodeKind.StartEvent, node.Kind);
			Assert.Empty(graph.Edges);
		}

		[Fact]
		public async Task GetGraphAsync_DanglingSequenceFlow_LeavesEdgeOut()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: DiagramWithDanglingFlow());

			var graph = await _service.GetGraphAsync(project.Id, null);

			Assert.DoesNotContain(graph.Edges, edge => edge.BpmnId == DanglingFlowId);
			Assert.Equal(2, graph.Edges.Count);
		}

		[Fact]
		public async Task GetGraphAsync_SpecificRevision_UsesThatRevision()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);
			await TestData.AddRevisionAsync(project, BpmnDiagramBuilder.Chain(1));

			var graph = await _service.GetGraphAsync(project.Id, 1);

			Assert.Equal(1, graph.RevisionNumber);
			Assert.Equal(12, graph.Nodes.Count);
		}

		[Fact]
		public async Task GetGraphAsync_NoRevision_UsesLatestRevision()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);
			await TestData.AddRevisionAsync(project, BpmnDiagramBuilder.Chain(1));

			var graph = await _service.GetGraphAsync(project.Id, null);

			Assert.Equal(2, graph.RevisionNumber);
			Assert.Equal(3, graph.Nodes.Count);
		}

		[Fact]
		public async Task GetGraphAsync_UnknownRevision_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<InvalidRequestException>(() => _service.GetGraphAsync(project.Id, 2));

			Assert.Equal(ErrorMessages.RevisionNotFound, exception.Message);
		}

		[Fact]
		public async Task GetGraphAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.GetGraphAsync(projectId, null));
		}

		[Fact]
		public async Task GetValidationReportAsync_DefaultDiagram_ReportsDv03AndNotOptimizable()
		{
			var project = await TestData.AddProjectAsync();

			var report = await _service.GetValidationReportAsync(project.Id, null);

			Assert.Equal(1, report.RevisionNumber);
			Assert.False(report.IsOptimizable);
			var issue = Assert.Single(report.Issues, issue => issue.Code == DiagramIssueCodes.EndEventMissing);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.False(string.IsNullOrWhiteSpace(issue.Message));
		}

		[Fact]
		public async Task GetValidationReportAsync_WorkedExample_IsOptimizableWithoutIssues()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);

			var report = await _service.GetValidationReportAsync(project.Id, null);

			Assert.True(report.IsOptimizable);
			Assert.Empty(report.Issues);
		}

		[Fact]
		public async Task GetValidationReportAsync_WarningsOnly_IsOptimizable()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: DiagramWithUnestimatedTask());

			var report = await _service.GetValidationReportAsync(project.Id, null);

			Assert.True(report.IsOptimizable);
			var issue = Assert.Single(report.Issues);
			Assert.Equal(DiagramIssueCodes.MissingEstimate, issue.Code);
			Assert.Equal(EIssueSeverity.Warning, issue.Severity);
			Assert.Equal(UnestimatedTaskId, issue.BpmnElementId);
		}

		[Fact]
		public async Task GetValidationReportAsync_DanglingSequenceFlow_ReportsDv05WithFlowId()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: DiagramWithDanglingFlow());

			var report = await _service.GetValidationReportAsync(project.Id, null);

			Assert.False(report.IsOptimizable);
			var issue = Assert.Single(report.Issues, issue => issue.Code == DiagramIssueCodes.DanglingSequenceFlow);
			Assert.Equal(DanglingFlowId, issue.BpmnElementId);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
		}

		[Fact]
		public async Task GetValidationReportAsync_SpecificRevision_UsesThatRevision()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var report = await _service.GetValidationReportAsync(project.Id, 1);

			Assert.Equal(1, report.RevisionNumber);
			Assert.False(report.IsOptimizable);
		}

		[Fact]
		public async Task GetValidationReportAsync_NoRevision_UsesLatestRevision()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var report = await _service.GetValidationReportAsync(project.Id, null);

			Assert.Equal(2, report.RevisionNumber);
			Assert.True(report.IsOptimizable);
		}

		[Fact]
		public async Task GetValidationReportAsync_UnknownRevision_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<InvalidRequestException>(() => _service.GetValidationReportAsync(project.Id, 5));

			Assert.Equal(ErrorMessages.RevisionNotFound, exception.Message);
		}

		[Fact]
		public async Task GetValidationReportAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.GetValidationReportAsync(projectId, null));
		}

		private static string DiagramWithDanglingFlow() =>
			new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("Work", "Work", 1, 100)
				.EndEvent("End")
				.Flow("FlowIn", "Start", "Work")
				.Flow("FlowOut", "Work", "End")
				.Flow(DanglingFlowId, "Work", "Missing")
				.Build();

		private static string DiagramWithUnestimatedTask() =>
			new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task(UnestimatedTaskId, "Unestimated", null, null)
				.EndEvent("End")
				.Flow("FlowIn", "Start", UnestimatedTaskId)
				.Flow("FlowOut", UnestimatedTaskId, "End")
				.Build();
	}
}
