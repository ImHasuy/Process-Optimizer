using ProjectOptimizerAPI.Lib.Helper;
using System.Net;
using System.Net.Http.Json;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Integration.Controller
{
	public sealed class SavedScenarioControllerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
	{
		private const string DuplicateNameMessage = "A saved scenario with this name already exists.";

		private static readonly string TwoScenarioDiagram = BpmnDiagramBuilder.SequentialExclusiveChoices(1);

		private readonly HttpClient _client = factory.CreateClient();

		private static string SavedScenariosUrl(Guid projectId) => $"/api/projects/{projectId}/saved-scenarios";

		private static string SavedScenarioUrl(Guid projectId, Guid id) => $"{SavedScenariosUrl(projectId)}/{id}";

		private static string UniqueName() => $"Saved {Guid.NewGuid():N}";

		private static object CreateBody(long scenarioIndex, string name, string? note = null) =>
			new { ScenarioIndex = scenarioIndex, Name = name, Note = note };

		private Task<SavedScenarioDTO> CreateSavedScenarioAsync(Guid projectId, string name, long scenarioIndex = 0) =>
			_client.PostAndReadDataAsync<SavedScenarioDTO>(SavedScenariosUrl(projectId), CreateBody(scenarioIndex, name));

		[Fact]
		public async Task CreateSavedScenario_ValidBody_ReturnsCreatedWithLocation()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var name = UniqueName();

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(projectId), CreateBody(1, name, "Worth keeping"));

			Assert.Equal(HttpStatusCode.Created, response.StatusCode);
			var body = await response.ReadApiResponseAsync<SavedScenarioDTO>();
			Assert.Equal((int)HttpStatusCode.Created, body.StatusCode);
			var saved = body.Data!;
			Assert.Equal(name, saved.Name);
			Assert.Equal("Worth keeping", saved.Note);
			Assert.Equal(2, saved.RevisionNumber);
			Assert.Equal(1, saved.ScenarioIndex);
			Assert.Equal(saved.ScenarioKey, saved.Snapshot.Key);
			Assert.True(saved.IsAvailableInLatestRevision);
			Assert.Equal(1, saved.LatestRevisionIndex);
			Assert.NotNull(response.Headers.Location);
			Assert.EndsWith(SavedScenarioUrl(projectId, saved.Id), response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task CreateSavedScenario_HundredCharacterNameWithSurroundingSpaces_CreatesTrimmedName()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var name = new string('s', 100);

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(projectId), CreateBody(0, $"  {name}  "));

			Assert.Equal(HttpStatusCode.Created, response.StatusCode);
			Assert.Equal(name, (await response.ReadApiResponseAsync<SavedScenarioDTO>()).Data!.Name);
		}

		[Fact]
		public async Task CreateSavedScenario_MissingName_ReturnsBadRequest()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(projectId), new { ScenarioIndex = 0 });

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			var body = await response.ReadApiErrorAsync();
			Assert.NotNull(body.Errors);
			Assert.Contains(nameof(SavedScenarioCreateDTO.Name), body.Errors!.Keys);
		}

		[Fact]
		public async Task CreateSavedScenario_WhitespaceName_ReturnsBadRequest()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(projectId), CreateBody(0, "   "));

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			Assert.Contains(nameof(SavedScenarioCreateDTO.Name), (await response.ReadApiErrorAsync()).Errors!.Keys);
		}

		[Fact]
		public async Task CreateSavedScenario_NegativeIndex_ReturnsBadRequest()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(projectId), CreateBody(-1, UniqueName()));

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			Assert.Contains(nameof(SavedScenarioCreateDTO.ScenarioIndex), (await response.ReadApiErrorAsync()).Errors!.Keys);
		}

		[Fact]
		public async Task CreateSavedScenario_MissingIndex_ReturnsBadRequest()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(projectId), new { Name = UniqueName() });

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			Assert.Contains(nameof(SavedScenarioCreateDTO.ScenarioIndex), (await response.ReadApiErrorAsync()).Errors!.Keys);
		}

		[Fact]
		public async Task CreateSavedScenario_ZeroRevision_ReturnsBadRequest()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(projectId), new { ScenarioIndex = 0, Revision = 0, Name = UniqueName() });

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			Assert.Contains(nameof(SavedScenarioCreateDTO.Revision), (await response.ReadApiErrorAsync()).Errors!.Keys);
		}

		[Fact]
		public async Task CreateSavedScenario_TooLongNote_ReturnsBadRequest()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(projectId), CreateBody(0, UniqueName(), new string('n', 1001)));

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			Assert.Contains(nameof(SavedScenarioCreateDTO.Note), (await response.ReadApiErrorAsync()).Errors!.Keys);
		}

		[Fact]
		public async Task CreateSavedScenario_IndexEqualToCount_ReturnsBadRequest()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(projectId), CreateBody(2, UniqueName()));

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			Assert.Equal(ErrorMessages.ScenarioNotFound, (await response.ReadApiErrorAsync()).Message);
		}

		[Fact]
		public async Task CreateSavedScenario_UnknownProject_ReturnsNotFound()
		{
			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(Guid.NewGuid()), CreateBody(0, UniqueName()));

			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		}

		[Fact]
		public async Task CreateSavedScenario_DuplicateName_ReturnsConflict()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var name = UniqueName();
			await CreateSavedScenarioAsync(projectId, name);

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(projectId), CreateBody(1, name.ToUpperInvariant()));

			Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
			Assert.Equal(DuplicateNameMessage, (await response.ReadApiErrorAsync()).Message);
		}

		[Fact]
		public async Task CreateSavedScenario_NewProjectWithErrors_ReturnsConflict()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(SavedScenariosUrl(project.Id), CreateBody(0, UniqueName()));

			Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
			Assert.Equal((int)HttpStatusCode.Conflict, (await response.ReadApiErrorAsync()).StatusCode);
		}

		[Fact]
		public async Task ListSavedScenarios_ExistingProject_ReturnsOkNewestFirst()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var first = await CreateSavedScenarioAsync(projectId, UniqueName());
			var second = await CreateSavedScenarioAsync(projectId, UniqueName(), 1);

			var response = await _client.GetAsync(SavedScenariosUrl(projectId));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.ReadApiResponseAsync<List<SavedScenarioListItemDTO>>();
			Assert.Equal((int)HttpStatusCode.OK, body.StatusCode);
			Assert.Equal([second.Id, first.Id], body.Data!.Select(item => item.Id));
			Assert.All(body.Data!, item => Assert.True(item.IsAvailableInLatestRevision));
		}

		[Fact]
		public async Task ListSavedScenarios_UnknownProject_ReturnsNotFound()
		{
			var response = await _client.GetAsync(SavedScenariosUrl(Guid.NewGuid()));

			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
			Assert.Equal((int)HttpStatusCode.NotFound, (await response.ReadApiErrorAsync()).StatusCode);
		}

		[Fact]
		public async Task GetSavedScenario_Existing_ReturnsOkWithSnapshot()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var saved = await CreateSavedScenarioAsync(projectId, UniqueName(), 1);

			var response = await _client.GetAsync(SavedScenarioUrl(projectId, saved.Id));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var result = (await response.ReadApiResponseAsync<SavedScenarioDTO>()).Data!;
			Assert.Equal(saved.Name, result.Name);
			Assert.Equal(saved.ScenarioKey, result.Snapshot.Key);
			Assert.Equal(projectId, result.Snapshot.ProjectId);
		}

		[Fact]
		public async Task GetSavedScenario_UnknownId_ReturnsNotFound()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);

			var response = await _client.GetAsync(SavedScenarioUrl(projectId, Guid.NewGuid()));

			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
			Assert.Contains("was not found", (await response.ReadApiErrorAsync()).Message);
		}

		[Fact]
		public async Task GetSavedScenario_IdOfOtherProject_ReturnsNotFound()
		{
			var ownerId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var otherId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var saved = await CreateSavedScenarioAsync(ownerId, UniqueName());

			var response = await _client.GetAsync(SavedScenarioUrl(otherId, saved.Id));

			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		}

		[Fact]
		public async Task UpdateSavedScenario_ValidBody_ReturnsNoContentAndGetShowsChange()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var saved = await CreateSavedScenarioAsync(projectId, UniqueName());
			var newName = UniqueName();

			var response = await _client.PutAsJsonAsync(SavedScenarioUrl(projectId, saved.Id), new { Name = newName, Note = "Updated" });

			Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
			var result = (await (await _client.GetAsync(SavedScenarioUrl(projectId, saved.Id))).ReadApiResponseAsync<SavedScenarioDTO>()).Data!;
			Assert.Equal(newName, result.Name);
			Assert.Equal("Updated", result.Note);
			Assert.Equal(saved.ScenarioKey, result.ScenarioKey);
			Assert.Equal(saved.Snapshot.TotalCost, result.Snapshot.TotalCost);
		}

		[Fact]
		public async Task UpdateSavedScenario_MissingName_ReturnsBadRequest()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var saved = await CreateSavedScenarioAsync(projectId, UniqueName());

			var response = await _client.PutAsJsonAsync(SavedScenarioUrl(projectId, saved.Id), new { Note = "No name" });

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			Assert.Contains(nameof(SavedScenarioUpdateDTO.Name), (await response.ReadApiErrorAsync()).Errors!.Keys);
		}

		[Fact]
		public async Task UpdateSavedScenario_NameOfOtherSavedScenario_ReturnsConflict()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var taken = await CreateSavedScenarioAsync(projectId, UniqueName());
			var saved = await CreateSavedScenarioAsync(projectId, UniqueName(), 1);

			var response = await _client.PutAsJsonAsync(SavedScenarioUrl(projectId, saved.Id), new { Name = $"  {taken.Name}  " });

			Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
			Assert.Equal(DuplicateNameMessage, (await response.ReadApiErrorAsync()).Message);
		}

		[Fact]
		public async Task UpdateSavedScenario_UnknownId_ReturnsNotFound()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);

			var response = await _client.PutAsJsonAsync(SavedScenarioUrl(projectId, Guid.NewGuid()), new { Name = UniqueName() });

			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		}

		[Fact]
		public async Task DeleteSavedScenario_Existing_ReturnsNoContent()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);
			var saved = await CreateSavedScenarioAsync(projectId, UniqueName());

			var response = await _client.DeleteAsync(SavedScenarioUrl(projectId, saved.Id));

			Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
			Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(SavedScenarioUrl(projectId, saved.Id))).StatusCode);
		}

		[Fact]
		public async Task DeleteSavedScenario_UnknownId_ReturnsNotFound()
		{
			var projectId = await _client.CreateProjectWithDiagramAsync(TwoScenarioDiagram);

			var response = await _client.DeleteAsync(SavedScenarioUrl(projectId, Guid.NewGuid()));

			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		}
	}
}
