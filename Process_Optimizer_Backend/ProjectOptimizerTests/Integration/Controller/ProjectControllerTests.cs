using System.Net;
using System.Net.Http.Json;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Integration.Controller
{
	public sealed class ProjectControllerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
	{
		private const string ProjectsUrl = "/api/projects";

		private readonly HttpClient _client = factory.CreateClient();

		private static string UniqueName() => $"Project {Guid.NewGuid():N}";

		private static string ProjectUrl(Guid id) => $"{ProjectsUrl}/{id}";

		private static object UpdateBody(string name, string? description = null, string? timeUnit = "Days", string? currency = "EUR") =>
			new { Name = name, Description = description, TimeUnit = timeUnit, Currency = currency };

		private async Task<ProjectDetailsDTO> GetProjectAsync(Guid id) =>
			(await (await _client.GetAsync(ProjectUrl(id))).ReadApiResponseAsync<ProjectDetailsDTO>()).Data!;

		[Fact]
		public async Task ListProjects_CreatedProject_ReturnsOkEnvelopeContainingIt()
		{
			var created = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(ProjectsUrl);

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.ReadApiResponseAsync<List<ProjectListItemDTO>>();
			Assert.Equal((int)HttpStatusCode.OK, body.StatusCode);
			Assert.Contains(body.Data!, item => item.Id == created.Id);
		}

		[Fact]
		public async Task CreateProject_ValidBody_ReturnsCreatedWithLocationAndRevisionOne()
		{
			var name = UniqueName();

			var response = await _client.PostAsJsonAsync(ProjectsUrl, new { Name = $"  {name}  ", Description = "Plan", TimeUnit = "Hours", Currency = "USD" });

			Assert.Equal(HttpStatusCode.Created, response.StatusCode);
			var body = await response.ReadApiResponseAsync<ProjectDetailsDTO>();
			Assert.Equal((int)HttpStatusCode.Created, body.StatusCode);
			var created = body.Data!;
			Assert.Equal(name, created.Name);
			Assert.Equal("Plan", created.Description);
			Assert.Equal(ETimeUnit.Hours, created.TimeUnit);
			Assert.Equal("USD", created.Currency);
			Assert.Equal(1, created.LatestRevision.RevisionNumber);
			Assert.False(created.LatestRevision.IsOptimizable);
			Assert.EndsWith(ProjectUrl(created.Id), response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task CreateProject_OnlyName_DefaultsToDaysAndEur()
		{
			var created = await _client.CreateProjectAsync();

			Assert.Equal(ETimeUnit.Days, created.TimeUnit);
			Assert.Equal("EUR", created.Currency);
		}

		[Fact]
		public async Task CreateProject_MissingName_ReturnsBadRequestWithNameError()
		{
			var response = await _client.PostAsJsonAsync(ProjectsUrl, new { Description = "No name" });

			await response.AssertValidationErrorAsync(nameof(ProjectCreateDTO.Name));
		}

		[Fact]
		public async Task CreateProject_WhitespaceName_ReturnsBadRequest()
		{
			var response = await _client.PostAsJsonAsync(ProjectsUrl, new { Name = "   " });

			await response.AssertValidationErrorAsync(nameof(ProjectCreateDTO.Name));
		}

		[Fact]
		public async Task CreateProject_NameTooLong_ReturnsBadRequest()
		{
			var response = await _client.PostAsJsonAsync(ProjectsUrl, new { Name = new string('n', 101) });

			await response.AssertValidationErrorAsync(nameof(ProjectCreateDTO.Name));
		}

		[Fact]
		public async Task CreateProject_HundredCharacterNameWithSurroundingSpaces_CreatesTrimmedName()
		{
			var name = Guid.NewGuid().ToString("N").PadRight(100, 'n');

			var response = await _client.PostAsJsonAsync(ProjectsUrl, new { Name = $"  {name}  " });

			Assert.Equal(HttpStatusCode.Created, response.StatusCode);
			Assert.Equal(name, (await response.ReadApiResponseAsync<ProjectDetailsDTO>()).Data!.Name);
		}

		[Fact]
		public async Task CreateProject_DescriptionTooLong_ReturnsBadRequest()
		{
			var response = await _client.PostAsJsonAsync(ProjectsUrl, new { Name = UniqueName(), Description = new string('d', 1001) });

			await response.AssertValidationErrorAsync(nameof(ProjectCreateDTO.Description));
		}

		[Fact]
		public async Task CreateProject_UnknownTimeUnit_ReturnsBadRequest()
		{
			var response = await _client.PostAsJsonAsync(ProjectsUrl, new { Name = UniqueName(), TimeUnit = "Months" });

			await response.AssertValidationErrorAsync(nameof(ProjectCreateDTO.TimeUnit));
		}

		[Fact]
		public async Task CreateProject_LowercaseCurrency_ReturnsBadRequest()
		{
			var response = await _client.PostAsJsonAsync(ProjectsUrl, new { Name = UniqueName(), Currency = "eur" });

			await response.AssertValidationErrorAsync(nameof(ProjectCreateDTO.Currency));
		}

		[Fact]
		public async Task CreateProject_DuplicateName_ReturnsConflict()
		{
			var existing = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(ProjectsUrl, new { Name = $" {existing.Name.ToUpperInvariant()} " });

			await response.AssertApiErrorAsync(HttpStatusCode.Conflict, "A project with this name already exists.");
		}

		[Fact]
		public async Task GetProject_Existing_ReturnsOkWithLatestRevision()
		{
			var created = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(ProjectUrl(created.Id));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var project = (await response.ReadApiResponseAsync<ProjectDetailsDTO>()).Data!;
			Assert.Equal(created.Name, project.Name);
			Assert.Equal(1, project.LatestRevision.RevisionNumber);
		}

		[Fact]
		public async Task GetProject_UnknownId_ReturnsNotFound()
		{
			var unknownId = Guid.NewGuid();

			var response = await _client.GetAsync(ProjectUrl(unknownId));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound, $"Project with id '{unknownId}' was not found.");
		}

		[Fact]
		public async Task UpdateProject_ValidBody_ReturnsNoContentAndGetShowsChange()
		{
			var created = await _client.CreateProjectAsync();
			var newName = UniqueName();

			var response = await _client.PutAsJsonAsync(ProjectUrl(created.Id), UpdateBody(newName, "Updated", "Weeks", "HUF"));

			Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
			var updated = await GetProjectAsync(created.Id);
			Assert.Equal(newName, updated.Name);
			Assert.Equal("Updated", updated.Description);
			Assert.Equal(ETimeUnit.Weeks, updated.TimeUnit);
			Assert.Equal("HUF", updated.Currency);
			Assert.Equal(1, updated.LatestRevision.RevisionNumber);
		}

		[Fact]
		public async Task UpdateProject_MissingTimeUnit_ReturnsBadRequest()
		{
			var created = await _client.CreateProjectAsync();

			var response = await _client.PutAsJsonAsync(ProjectUrl(created.Id), UpdateBody(created.Name, timeUnit: null));

			await response.AssertValidationErrorAsync(nameof(ProjectUpdateDTO.TimeUnit));
		}

		[Fact]
		public async Task UpdateProject_MissingCurrency_ReturnsBadRequest()
		{
			var created = await _client.CreateProjectAsync();

			var response = await _client.PutAsJsonAsync(ProjectUrl(created.Id), UpdateBody(created.Name, currency: null));

			await response.AssertValidationErrorAsync(nameof(ProjectUpdateDTO.Currency));
		}

		[Fact]
		public async Task UpdateProject_UnknownId_ReturnsNotFound()
		{
			var response = await _client.PutAsJsonAsync(ProjectUrl(Guid.NewGuid()), UpdateBody(UniqueName()));

			await response.AssertApiErrorContainingAsync(HttpStatusCode.NotFound, "was not found");
		}

		[Fact]
		public async Task UpdateProject_DuplicateName_ReturnsConflict()
		{
			var existing = await _client.CreateProjectAsync();
			var project = await _client.CreateProjectAsync();

			var response = await _client.PutAsJsonAsync(ProjectUrl(project.Id), UpdateBody(existing.Name.ToLowerInvariant()));

			await response.AssertApiErrorAsync(HttpStatusCode.Conflict, "A project with this name already exists.");
			Assert.Equal(project.Name, (await GetProjectAsync(project.Id)).Name);
		}

		[Fact]
		public async Task DeleteProject_Existing_ReturnsNoContentAndGetReturnsNotFound()
		{
			var created = await _client.CreateProjectAsync();

			var response = await _client.DeleteAsync(ProjectUrl(created.Id));

			Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
			Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(ProjectUrl(created.Id))).StatusCode);
		}

		[Fact]
		public async Task DeleteProject_UnknownId_ReturnsNotFound()
		{
			var response = await _client.DeleteAsync(ProjectUrl(Guid.NewGuid()));

			await response.AssertApiErrorContainingAsync(HttpStatusCode.NotFound, "was not found");
		}
	}
}
