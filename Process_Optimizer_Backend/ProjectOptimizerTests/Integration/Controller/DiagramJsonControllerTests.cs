using System.Net;
using System.Net.Http.Json;
using System.Text;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Integration.Controller
{
	public sealed class DiagramJsonControllerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
	{
		private const string StaleRevisionMessage = "The diagram was changed by another session. Reload it before saving.";
		private const string JsonContentType = "application/json";

		private readonly HttpClient _client = factory.CreateClient();

		[Fact]
		public async Task SaveJsonDiagram_FrontendExport_ReturnsCreatedWithLocationToRevision()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsync(JsonRevisionsUrl(project.Id), new StringContent(ProcessJsonFixtures.FrontendReadmeExportJson, Encoding.UTF8, JsonContentType));

			Assert.Equal(HttpStatusCode.Created, response.StatusCode);
			var body = await response.ReadApiResponseAsync<DiagramRevisionDTO>();
			Assert.Equal((int)HttpStatusCode.Created, body.StatusCode);
			Assert.Equal(2, body.Data!.RevisionNumber);
			Assert.True(body.Data.IsOptimizable);
			Assert.Equal(0, body.Data.WarningCount);
			Assert.EndsWith($"/api/projects/{project.Id}/diagram/revisions/2", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task SaveJsonDiagram_ThenLatestDiagram_ReturnsGeneratedBpmnXml()
		{
			var project = await _client.CreateProjectAsync();
			await _client.PostAsync(JsonRevisionsUrl(project.Id), new StringContent(ProcessJsonFixtures.FrontendReadmeExportJson, Encoding.UTF8, JsonContentType));

			var latest = (await (await _client.GetAsync($"/api/projects/{project.Id}/diagram")).ReadApiResponseAsync<DiagramRevisionDTO>()).Data!;

			Assert.Equal(2, latest.RevisionNumber);
			Assert.StartsWith("<?xml", latest.BpmnXml);
			Assert.Contains("bpmndi:BPMNShape", latest.BpmnXml);
			Assert.Contains("optimizer:time=\"15m\"", latest.BpmnXml);
		}

		[Fact]
		public async Task SaveJsonDiagram_ExclusiveChoice_EnablesScenarioEndpoints()
		{
			var project = await _client.CreateProjectAsync();
			await _client.PostAsJsonAsync(JsonRevisionsUrl(project.Id), JsonRequest(ProcessJsonFixtures.ExclusiveChoiceStates(), ProcessJsonFixtures.ExclusiveChoiceBranches(), 1));

			var response = await _client.GetAsync($"/api/projects/{project.Id}/scenario-count");

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Equal(2, (await response.ReadApiResponseAsync<ScenarioCountDTO>()).Data!.Count);
		}

		[Fact]
		public async Task SaveJsonDiagram_StaleBaseRevision_ReturnsConflict()
		{
			var project = await _client.CreateProjectAsync();
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample, 1);

			var response = await _client.PostAsJsonAsync(JsonRevisionsUrl(project.Id), JsonRequest(ProcessJsonFixtures.SampleStates(), ProcessJsonFixtures.SampleBranches(), 1));

			await response.AssertApiErrorAsync(HttpStatusCode.Conflict, StaleRevisionMessage);
		}

		[Fact]
		public async Task SaveJsonDiagram_MissingBaseRevision_ReturnsValidationError()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(JsonRevisionsUrl(project.Id), new { States = ProcessJsonFixtures.SampleStates(), Branches = ProcessJsonFixtures.SampleBranches() });

			await response.AssertValidationErrorAsync("BaseRevision");
		}

		[Fact]
		public async Task SaveJsonDiagram_BranchWithoutSource_ReturnsValidationError()
		{
			var project = await _client.CreateProjectAsync();
			var branches = ProcessJsonFixtures.SampleBranches();
			branches[0].Source = null!;

			var response = await _client.PostAsJsonAsync(JsonRevisionsUrl(project.Id), JsonRequest(ProcessJsonFixtures.SampleStates(), branches, 1));

			await response.AssertValidationErrorAsync("Source");
		}

		[Fact]
		public async Task SaveJsonDiagram_DuplicateId_ReturnsBadRequestEnvelope()
		{
			var project = await _client.CreateProjectAsync();
			var states = ProcessJsonFixtures.SampleStates();
			states[2].Id = "Activity_1";

			var response = await _client.PostAsJsonAsync(JsonRevisionsUrl(project.Id), JsonRequest(states, ProcessJsonFixtures.SampleBranches(), 1));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest, "The id 'Activity_1' is used more than once.");
		}

		[Fact]
		public async Task SaveJsonDiagram_UnknownProject_ReturnsNotFoundEnvelope()
		{
			var response = await _client.PostAsJsonAsync(JsonRevisionsUrl(Guid.NewGuid()), JsonRequest(ProcessJsonFixtures.SampleStates(), ProcessJsonFixtures.SampleBranches(), 1));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		private static string JsonRevisionsUrl(Guid projectId) => $"/api/projects/{projectId}/diagram/revisions/json";

		private static object JsonRequest(List<ProcessStateDTO> states, List<ProcessBranchDTO> branches, int baseRevision) =>
			new { States = states, Branches = branches, BaseRevision = baseRevision };
	}
}
