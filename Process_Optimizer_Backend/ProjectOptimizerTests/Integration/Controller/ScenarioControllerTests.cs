using ProjectOptimizerAPI.Lib.Helper;
using System.Net;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Integration.Controller
{
	public sealed class ScenarioControllerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
	{
		private readonly HttpClient _client = factory.CreateClient();

		[Fact]
		public async Task GetScenarioCount_WorkedExample_ReturnsTwo()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(ScenarioCountUrl(projectId));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.ReadApiResponseAsync<ScenarioCountDTO>();
			Assert.Equal((int)HttpStatusCode.OK, body.StatusCode);
			Assert.Equal(2, body.Data!.Count);
			Assert.Equal(1, body.Data.OrderingVersion);
		}

		[Fact]
		public async Task GetScenarioCount_NewProject_ReturnsConflict()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(ScenarioCountUrl(project.Id));

			await response.AssertApiErrorAsync(HttpStatusCode.Conflict, ErrorMessages.DiagramHasErrors);
		}

		[Fact]
		public async Task GetScenarioCount_TooManyScenarios_ReturnsConflict()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnDiagramBuilder.SequentialExclusiveChoices(54));

			var response = await _client.GetAsync(ScenarioCountUrl(projectId));

			await response.AssertApiErrorAsync(HttpStatusCode.Conflict, ErrorMessages.TooManyScenarios);
		}

		[Fact]
		public async Task GetScenarioCount_UnknownProject_ReturnsNotFound()
		{
			var response = await _client.GetAsync(ScenarioCountUrl(Guid.NewGuid()));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task GetScenario_IndexOne_ReturnsG3EqualsF()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(ScenarioUrl(projectId, 1));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var scenario = (await response.ReadApiResponseAsync<ScenarioDTO>()).Data!;
			Assert.Equal(projectId, scenario.ProjectId);
			Assert.Equal(1, scenario.Index);
			Assert.Equal("G3=F", scenario.Key);
			Assert.Equal(20m, scenario.TotalDuration);
			Assert.Equal(28m, scenario.SequentialDuration);
			Assert.Equal(25000m, scenario.TotalCost);
			Assert.Equal(2, scenario.PathCount);
		}

		[Fact]
		public async Task GetScenario_IndexTwo_ReturnsNotFound()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(ScenarioUrl(projectId, 2));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound, ErrorMessages.ScenarioNotFound);
		}

		[Fact]
		public async Task GetScenario_NegativeIndex_ReturnsBadRequest()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(ScenarioUrl(projectId, -1));

			await response.AssertValidationErrorAsync("index");
		}

		[Fact]
		public async Task GetScenario_DiagramOverwrittenWithErrors_ReturnsConflict()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);
			await _client.SaveDiagramAsync(projectId, BpmnFixtures.DisconnectedDiagram());

			var response = await _client.GetAsync(ScenarioUrl(projectId, 0));

			await response.AssertApiErrorAsync(HttpStatusCode.Conflict, ErrorMessages.DiagramHasErrors);
		}

		[Fact]
		public async Task GetScenario_UnknownProject_ReturnsNotFound()
		{
			var response = await _client.GetAsync(ScenarioUrl(Guid.NewGuid(), 0));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task GetAllScenarios_WorkedExample_ReturnsBothScenarios()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(ScenariosUrl(projectId));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var scenarios = (await response.ReadApiResponseAsync<List<ScenarioDTO>>()).Data!;
			Assert.Equal(["G3=E", "G3=F"], scenarios.Select(scenario => scenario.Key));
			Assert.All(scenarios, scenario => Assert.NotEmpty(scenario.CriticalPath));
		}

		[Fact]
		public async Task GetAllScenarios_MoreThanThousandScenarios_ReturnsConflict()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnDiagramBuilder.SequentialExclusiveChoices(10));

			var response = await _client.GetAsync(ScenariosUrl(projectId));

			await response.AssertApiErrorAsync(HttpStatusCode.Conflict, ErrorMessages.TooManyScenariosToList);
		}

		[Fact]
		public async Task GetAllScenarios_NewProject_ReturnsConflict()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(ScenariosUrl(project.Id));

			await response.AssertApiErrorAsync(HttpStatusCode.Conflict, ErrorMessages.DiagramHasErrors);
		}

		[Fact]
		public async Task GetAllScenarios_UnknownProject_ReturnsNotFound()
		{
			var response = await _client.GetAsync(ScenariosUrl(Guid.NewGuid()));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task GetResourceLimits_WorkedExample_ReturnsGoldenLimits()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(ResourceLimitsUrl(projectId));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var limits = (await response.ReadApiResponseAsync<ResourceLimitsDTO>()).Data!;
			Assert.Equal(EResourceLimitBasis.Planned, limits.Basis);
			Assert.Equal(25000m, limits.MaxCost);
			Assert.Equal(1, limits.MaxCostScenario!.Index);
			Assert.Equal(23000m, limits.MinCost);
			Assert.Equal(21m, limits.MaxCriticalPathDuration);
			Assert.Equal(20m, limits.MinCriticalPathDuration);
			Assert.Equal(25m, limits.MaxDurationBound);
			Assert.Equal(29m, limits.MaxSequentialDuration);
			Assert.Equal(2, limits.ScenarioCount);
		}

		[Fact]
		public async Task GetResourceLimits_PessimisticBasis_ReturnsPessimisticBasis()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync($"{ResourceLimitsUrl(projectId)}?basis=Pessimistic");

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var limits = (await response.ReadApiResponseAsync<ResourceLimitsDTO>()).Data!;
			Assert.Equal(EResourceLimitBasis.Pessimistic, limits.Basis);
		}

		[Fact]
		public async Task GetResourceLimits_TooManyScenarios_ReturnsLimitsWithoutScenarioCount()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnDiagramBuilder.SequentialExclusiveChoices(54));

			var response = await _client.GetAsync(ResourceLimitsUrl(projectId));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var limits = (await response.ReadApiResponseAsync<ResourceLimitsDTO>()).Data!;
			Assert.Null(limits.ScenarioCount);
			Assert.Null(limits.MaxCostScenario);
		}

		[Fact]
		public async Task GetResourceLimits_InvalidBasis_ReturnsBadRequest()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync($"{ResourceLimitsUrl(projectId)}?basis=Optimistic");

			await response.AssertValidationErrorAsync("Basis");
		}

		[Fact]
		public async Task GetResourceLimits_NewProject_ReturnsConflict()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(ResourceLimitsUrl(project.Id));

			await response.AssertApiErrorAsync(HttpStatusCode.Conflict, ErrorMessages.DiagramHasErrors);
		}

		[Fact]
		public async Task GetResourceLimits_UnknownProject_ReturnsNotFound()
		{
			var response = await _client.GetAsync(ResourceLimitsUrl(Guid.NewGuid()));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		private static string ScenarioCountUrl(Guid projectId) => $"/api/projects/{projectId}/scenario-count";

		private static string ScenariosUrl(Guid projectId) => $"/api/projects/{projectId}/scenarios";

		private static string ScenarioUrl(Guid projectId, long index) => $"{ScenariosUrl(projectId)}/{index}";

		private static string ResourceLimitsUrl(Guid projectId) => $"/api/projects/{projectId}/resource-limits";
	}
}
