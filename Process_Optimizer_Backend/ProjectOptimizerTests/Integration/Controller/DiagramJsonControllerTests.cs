using System.Net;
using System.Net.Http.Json;
using System.Text;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model.Process;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Integration.Controller
{
	public sealed class DiagramJsonControllerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
	{
		private const string JsonContentType = "application/json";

		private readonly HttpClient _client = factory.CreateClient();

		[Fact]
		public async Task SaveJsonDiagram_FrontendExport_ReturnsOkWithDiagram()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PutAsync(JsonDiagramUrl(project.Id), new StringContent(ProcessJsonFixtures.FrontendReadmeExportJson, Encoding.UTF8, JsonContentType));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.ReadApiResponseAsync<DiagramDTO>();
			Assert.Equal((int)HttpStatusCode.OK, body.StatusCode);
			Assert.Equal(3, body.Data!.FlowNodeCount);
			Assert.True(body.Data.IsOptimizable);
			Assert.Equal(0, body.Data.WarningCount);
		}

		[Fact]
		public async Task SaveJsonDiagram_ThenGetDiagram_ReturnsGeneratedBpmnXml()
		{
			var project = await _client.CreateProjectAsync();
			await _client.PutAsync(JsonDiagramUrl(project.Id), new StringContent(ProcessJsonFixtures.FrontendReadmeExportJson, Encoding.UTF8, JsonContentType));

			var diagram = (await (await _client.GetAsync($"/api/projects/{project.Id}/diagram")).ReadApiResponseAsync<DiagramDTO>()).Data!;

			Assert.StartsWith("<?xml", diagram.BpmnXml);
			Assert.Contains("bpmndi:BPMNShape", diagram.BpmnXml);
			Assert.Contains("optimizer:time=\"15m\"", diagram.BpmnXml);
		}

		[Fact]
		public async Task SaveJsonDiagram_ExclusiveChoice_EnablesScenarioEndpoints()
		{
			var project = await _client.CreateProjectAsync();
			await _client.PutAsJsonAsync(JsonDiagramUrl(project.Id), JsonRequest(ProcessJsonFixtures.ExclusiveChoiceStates(), ProcessJsonFixtures.ExclusiveChoiceBranches()));

			var response = await _client.GetAsync($"/api/projects/{project.Id}/scenario-count");

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Equal(2, (await response.ReadApiResponseAsync<ScenarioCountDTO>()).Data!.Count);
		}

		[Fact]
		public async Task SaveJsonDiagram_AfterXmlSave_OverwritesDiagram()
		{
			var project = await _client.CreateProjectAsync();
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample);

			var response = await _client.PutAsJsonAsync(JsonDiagramUrl(project.Id), JsonRequest(ProcessJsonFixtures.SampleStates(), ProcessJsonFixtures.SampleBranches()));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var diagram = (await (await _client.GetAsync($"/api/projects/{project.Id}/diagram")).ReadApiResponseAsync<DiagramDTO>()).Data!;
			Assert.Equal(3, diagram.FlowNodeCount);
			Assert.NotEqual(BpmnFixtures.WorkedExample, diagram.BpmnXml);
		}

		[Fact]
		public async Task SaveJsonDiagram_BranchWithoutSource_ReturnsValidationError()
		{
			var project = await _client.CreateProjectAsync();
			var branches = ProcessJsonFixtures.SampleBranches();
			branches[0].Source = null!;

			var response = await _client.PutAsJsonAsync(JsonDiagramUrl(project.Id), JsonRequest(ProcessJsonFixtures.SampleStates(), branches));

			await response.AssertValidationErrorAsync("Source");
		}

		[Fact]
		public async Task SaveJsonDiagram_DuplicateId_ReturnsBadRequestEnvelope()
		{
			var project = await _client.CreateProjectAsync();
			var states = ProcessJsonFixtures.SampleStates();
			states[2].Id = "Activity_1";

			var response = await _client.PutAsJsonAsync(JsonDiagramUrl(project.Id), JsonRequest(states, ProcessJsonFixtures.SampleBranches()));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest, "The id 'Activity_1' is used more than once.");
		}

		[Fact]
		public async Task SaveJsonDiagram_UnknownProject_ReturnsNotFoundEnvelope()
		{
			var response = await _client.PutAsJsonAsync(JsonDiagramUrl(Guid.NewGuid()), JsonRequest(ProcessJsonFixtures.SampleStates(), ProcessJsonFixtures.SampleBranches()));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		private static string JsonDiagramUrl(Guid projectId) => $"/api/projects/{projectId}/diagram/json";

		private static object JsonRequest(List<ProcessState> states, List<ProcessBranch> branches) =>
			new { States = states, Branches = branches };
	}
}
