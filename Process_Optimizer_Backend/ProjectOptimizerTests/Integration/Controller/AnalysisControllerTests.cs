using ProjectOptimizerAPI.Lib.Helper;
using System.Net;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Integration.Controller
{
	public sealed class AnalysisControllerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
	{
		private const string GraphResource = "graph";
		private const string ValidationResource = "validation";
		private const string EndEventMissingCode = "DV-03";

		private readonly HttpClient _client = factory.CreateClient();

		[Fact]
		public async Task GetGraph_NewProject_ReturnsOkWithStartEvent()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(AnalysisUrl(project.Id, GraphResource));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.ReadApiResponseAsync<ProcessGraphDTO>();
			Assert.Equal((int)HttpStatusCode.OK, body.StatusCode);
			Assert.Equal(1, body.Data!.RevisionNumber);
			var node = Assert.Single(body.Data.Nodes);
			Assert.Equal(EFlowNodeKind.StartEvent, node.Kind);
		}

		[Fact]
		public async Task GetGraph_WorkedExampleRevision_ReturnsNodesAndEdges()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(AnalysisUrl(projectId, GraphResource));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var graph = (await response.ReadApiResponseAsync<ProcessGraphDTO>()).Data!;
			Assert.Equal(2, graph.RevisionNumber);
			Assert.Equal(12, graph.Nodes.Count);
			Assert.Equal(13, graph.Edges.Count);
			Assert.Equal(new[] { "B", "C" }, graph.Nodes.Single(node => node.BpmnId == "G2").DependsOn);
		}

		[Fact]
		public async Task GetGraph_EarlierRevision_ReturnsThatRevision()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(AnalysisUrl(projectId, GraphResource, 1));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var graph = (await response.ReadApiResponseAsync<ProcessGraphDTO>()).Data!;
			Assert.Equal(1, graph.RevisionNumber);
			Assert.Single(graph.Nodes);
		}

		[Fact]
		public async Task GetGraph_RevisionZero_ReturnsBadRequestWithErrors()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(AnalysisUrl(project.Id, GraphResource, 0));

			await response.AssertValidationErrorAsync("revision");
		}

		[Fact]
		public async Task GetGraph_UnknownRevision_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(AnalysisUrl(project.Id, GraphResource, 7));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest, ErrorMessages.RevisionNotFound);
		}

		[Fact]
		public async Task GetGraph_UnknownProject_ReturnsNotFound()
		{
			var projectId = Guid.NewGuid();

			var response = await _client.GetAsync(AnalysisUrl(projectId, GraphResource));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task GetValidationReport_NewProject_ReturnsDv03Issue()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(AnalysisUrl(project.Id, ValidationResource));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var report = (await response.ReadApiResponseAsync<ValidationReportDTO>()).Data!;
			Assert.Equal(1, report.RevisionNumber);
			Assert.False(report.IsOptimizable);
			var issue = Assert.Single(report.Issues, issue => issue.Code == EndEventMissingCode);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
		}

		[Fact]
		public async Task GetValidationReport_WorkedExampleRevision_IsOptimizable()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(AnalysisUrl(projectId, ValidationResource, 2));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var report = (await response.ReadApiResponseAsync<ValidationReportDTO>()).Data!;
			Assert.Equal(2, report.RevisionNumber);
			Assert.True(report.IsOptimizable);
			Assert.Empty(report.Issues);
		}

		[Fact]
		public async Task GetValidationReport_RevisionZero_ReturnsBadRequestWithErrors()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(AnalysisUrl(project.Id, ValidationResource, 0));

			await response.AssertValidationErrorAsync("revision");
		}

		[Fact]
		public async Task GetValidationReport_UnknownRevision_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(AnalysisUrl(project.Id, ValidationResource, 3));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest, ErrorMessages.RevisionNotFound);
		}

		[Fact]
		public async Task GetValidationReport_UnknownProject_ReturnsNotFound()
		{
			var projectId = Guid.NewGuid();

			var response = await _client.GetAsync(AnalysisUrl(projectId, ValidationResource));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		private static string AnalysisUrl(Guid projectId, string resource, int? revision = null) =>
			revision is null
				? $"/api/projects/{projectId}/{resource}"
				: $"/api/projects/{projectId}/{resource}?revision={revision}";
	}
}
