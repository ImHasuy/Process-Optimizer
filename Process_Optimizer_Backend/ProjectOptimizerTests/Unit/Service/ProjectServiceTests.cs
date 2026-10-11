using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Service;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerLib.Model;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Service
{
	public sealed class ProjectServiceTests : ServiceTestBase
	{
		private ProjectService CreateService() => new(UnitOfWork, ProjectAccessGuard, Analysis.RevisionFactory, TimeProvider);

		private static ProjectCreateDTO CreateRequest(string name, string? description = null, ETimeUnit? timeUnit = null, string? currency = null) =>
			new() { Name = name, Description = description, TimeUnit = timeUnit, Currency = currency };

		private static ProjectUpdateDTO UpdateRequest(string name, string? description = null, ETimeUnit timeUnit = ETimeUnit.Days, string currency = "EUR") =>
			new() { Name = name, Description = description, TimeUnit = timeUnit, Currency = currency };

		private async Task<List<ProjectDetailsDTO>> CreateProjectsOneHourApartAsync(params string[] names)
		{
			var service = CreateService();
			var projects = new List<ProjectDetailsDTO>();
			foreach (var name in names)
			{
				projects.Add(await service.CreateProjectAsync(CreateRequest(name)));
				TimeProvider.Advance(TimeSpan.FromHours(1));
			}

			return projects;
		}

		private Task<List<DiagramRevision>> LoadRevisionsAsync(Guid projectId) =>
			DbContext.DiagramRevisions.AsNoTracking().Where(revision => revision.ProjectId == projectId).OrderBy(revision => revision.RevisionNumber).ToListAsync();

		private static List<string> NamesOf(List<ProjectListItemDTO> result) => result.Select(item => item.Name).ToList();

		[Fact]
		public async Task CreateProjectAsync_ValidRequest_CreatesRevisionOneWithDefaultDiagram()
		{
			var project = await CreateService().CreateProjectAsync(CreateRequest("Launch"));

			var revision = Assert.Single(await LoadRevisionsAsync(project.Id));
			Assert.Equal(1, revision.RevisionNumber);
			Assert.Equal(DefaultDiagram.Xml, revision.BpmnXml);
			Assert.Equal(1, project.LatestRevision.RevisionNumber);
			Assert.Equal(FixedNow.UtcDateTime, project.CreatedAt);
			Assert.Equal(FixedNow.UtcDateTime, project.UpdatedAt);
		}

		[Fact]
		public async Task CreateProjectAsync_ExplicitSettings_StoresThem()
		{
			var project = await CreateService().CreateProjectAsync(CreateRequest("Launch", "Release plan", ETimeUnit.Hours, "USD"));

			Assert.Equal("Launch", project.Name);
			Assert.Equal("Release plan", project.Description);
			Assert.Equal(ETimeUnit.Hours, project.TimeUnit);
			Assert.Equal("USD", project.Currency);
		}

		[Fact]
		public async Task CreateProjectAsync_OmittedTimeUnitAndCurrency_DefaultsToDaysAndEur()
		{
			var project = await CreateService().CreateProjectAsync(CreateRequest("Launch"));

			Assert.Equal(ETimeUnit.Days, project.TimeUnit);
			Assert.Equal("EUR", project.Currency);
		}

		[Fact]
		public async Task CreateProjectAsync_NameWithSurroundingSpaces_StoresTrimmedName()
		{
			var project = await CreateService().CreateProjectAsync(CreateRequest("  Launch  "));

			Assert.Equal("Launch", project.Name);
			Assert.Equal("LAUNCH", (await LoadProjectAsync(project.Id)).NormalizedName);
		}

		[Fact]
		public async Task CreateProjectAsync_DuplicateNameInOtherCasing_ThrowsConflict()
		{
			await TestData.AddProjectAsync("Launch");

			await Assert.ThrowsAsync<ConflictException>(() => CreateService().CreateProjectAsync(CreateRequest(" LAUNCH ")));
		}

		[Fact]
		public async Task CreateProjectAsync_DuplicateName_PersistsNothing()
		{
			await TestData.AddProjectAsync("Launch");

			await Assert.ThrowsAsync<ConflictException>(() => CreateService().CreateProjectAsync(CreateRequest("launch")));

			Assert.Equal(1, await DbContext.Projects.CountAsync());
			Assert.Equal(1, await DbContext.DiagramRevisions.CountAsync());
		}

		[Fact]
		public async Task ListProjectsAsync_SeveralProjects_ReturnsUpdatedAtDescending()
		{
			await CreateProjectsOneHourApartAsync("First", "Second", "Third");

			var result = await CreateService().ListProjectsAsync();

			Assert.Equal(new[] { "Third", "Second", "First" }, NamesOf(result));
		}

		[Fact]
		public async Task ListProjectsAsync_OptimizableLatestRevision_IncludesStoredLimits()
		{
			var project = await TestData.AddProjectAsync("Worked example", BpmnFixtures.WorkedExample);
			var revision = Assert.Single(await LoadRevisionsAsync(project.Id));

			var item = Assert.Single(await CreateService().ListProjectsAsync());

			Assert.True(item.IsOptimizable);
			Assert.NotNull(item.MaxCost);
			Assert.Equal(revision.MaxCost, item.MaxCost);
			Assert.Equal(revision.MaxDurationBound, item.MaxDurationBound);
			Assert.Equal(1, item.LatestRevisionNumber);
		}

		[Fact]
		public async Task ListProjectsAsync_DefaultDiagram_IsNotOptimizableWithoutLimits()
		{
			await TestData.AddProjectAsync("Empty");

			var item = Assert.Single(await CreateService().ListProjectsAsync());

			Assert.False(item.IsOptimizable);
			Assert.Null(item.MaxCost);
			Assert.Null(item.MaxDurationBound);
		}

		[Fact]
		public async Task ListProjectsAsync_LaterRevisionOptimizable_UsesLatestRevision()
		{
			var project = await TestData.AddProjectAsync("Evolving");
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var item = Assert.Single(await CreateService().ListProjectsAsync());

			Assert.Equal(2, item.LatestRevisionNumber);
			Assert.True(item.IsOptimizable);
			Assert.NotNull(item.MaxCost);
		}

		[Fact]
		public async Task GetProjectAsync_ExistingProject_ReturnsLatestRevisionSummary()
		{
			var project = await TestData.AddProjectAsync("Evolving");
			var latest = await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var details = await CreateService().GetProjectAsync(project.Id);

			Assert.Equal(project.Id, details.Id);
			Assert.Equal("Evolving", details.Name);
			Assert.Equal(2, details.LatestRevision.RevisionNumber);
			Assert.Equal(latest.FlowNodeCount, details.LatestRevision.FlowNodeCount);
			Assert.Equal(latest.SizeBytes, details.LatestRevision.SizeBytes);
			Assert.True(details.LatestRevision.IsOptimizable);
			Assert.Equal(latest.MaxCost, details.MaxCost);
			Assert.Equal(latest.MaxDurationBound, details.MaxDurationBound);
		}

		[Fact]
		public async Task GetProjectAsync_UnknownId_ThrowsNotFound()
		{
			var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetProjectAsync(Guid.NewGuid()));

			Assert.Contains("was not found", exception.Message);
		}

		[Fact]
		public async Task UpdateProjectAsync_ValidRequest_ReplacesFieldsAndUpdatesTimestamp()
		{
			var project = await TestData.AddProjectAsync("Launch");
			TimeProvider.Advance(TimeSpan.FromHours(3));

			await CreateService().UpdateProjectAsync(project.Id, UpdateRequest("  Relaunch ", "New plan", ETimeUnit.Weeks, "HUF"));

			var stored = await LoadProjectAsync(project.Id);
			Assert.Equal("Relaunch", stored.Name);
			Assert.Equal("RELAUNCH", stored.NormalizedName);
			Assert.Equal("New plan", stored.Description);
			Assert.Equal(ETimeUnit.Weeks, stored.TimeUnit);
			Assert.Equal("HUF", stored.Currency);
			Assert.Equal(FixedNow.UtcDateTime.AddHours(3), stored.UpdatedAt);
			Assert.Equal(FixedNow.UtcDateTime, stored.CreatedAt);
		}

		[Fact]
		public async Task UpdateProjectAsync_NullDescription_ClearsDescription()
		{
			var created = await CreateService().CreateProjectAsync(CreateRequest("Launch", "Old plan"));

			await CreateService().UpdateProjectAsync(created.Id, UpdateRequest("Launch"));

			Assert.Null((await LoadProjectAsync(created.Id)).Description);
		}

		[Fact]
		public async Task UpdateProjectAsync_OwnNameInOtherCasing_DoesNotConflict()
		{
			var project = await TestData.AddProjectAsync("Launch");

			await CreateService().UpdateProjectAsync(project.Id, UpdateRequest("LAUNCH"));

			Assert.Equal("LAUNCH", (await LoadProjectAsync(project.Id)).Name);
		}

		[Fact]
		public async Task UpdateProjectAsync_NameOfOtherProject_ThrowsConflict()
		{
			await TestData.AddProjectAsync("Launch");
			var project = await TestData.AddProjectAsync("Other");

			await Assert.ThrowsAsync<ConflictException>(() => CreateService().UpdateProjectAsync(project.Id, UpdateRequest(" launch ")));

			Assert.Equal("Other", (await LoadProjectAsync(project.Id)).Name);
		}

		[Fact]
		public async Task UpdateProjectAsync_ChangedTimeUnitAndCurrency_DoesNotModifyRevisions()
		{
			var project = await TestData.AddProjectAsync("Launch", BpmnFixtures.WorkedExample);
			var before = Assert.Single(await LoadRevisionsAsync(project.Id));

			await CreateService().UpdateProjectAsync(project.Id, UpdateRequest("Launch", timeUnit: ETimeUnit.Minutes, currency: "USD"));

			var after = Assert.Single(await LoadRevisionsAsync(project.Id));
			Assert.Equal(before.BpmnXml, after.BpmnXml);
			Assert.Equal(before.ContentSha256, after.ContentSha256);
			Assert.Equal(before.MaxCost, after.MaxCost);
			Assert.Equal(before.MaxDurationBound, after.MaxDurationBound);
			Assert.Equal(1, (await LoadProjectAsync(project.Id)).LatestRevisionNumber);
		}

		[Fact]
		public async Task UpdateProjectAsync_UnknownId_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() => CreateService().UpdateProjectAsync(Guid.NewGuid(), UpdateRequest("Launch")));
		}

		[Fact]
		public async Task DeleteProjectAsync_ExistingProject_RemovesRevisionsAndSavedScenarios()
		{
			var project = await TestData.AddProjectAsync("Launch", BpmnFixtures.WorkedExample);
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);
			await TestData.AddSavedScenarioAsync(project, 1, "Cheapest");
			DbContext.ChangeTracker.Clear();

			await CreateService().DeleteProjectAsync(project.Id);

			Assert.False(await DbContext.Projects.AnyAsync(stored => stored.Id == project.Id));
			Assert.False(await DbContext.DiagramRevisions.AnyAsync(revision => revision.ProjectId == project.Id));
			Assert.False(await DbContext.SavedScenarios.AnyAsync(scenario => scenario.ProjectId == project.Id));
		}

		[Fact]
		public async Task DeleteProjectAsync_ExistingProject_LeavesOtherProjectsUntouched()
		{
			var deleted = await TestData.AddProjectAsync("Deleted");
			var kept = await TestData.AddProjectAsync("Kept");
			await TestData.AddSavedScenarioAsync(kept, 1, "Kept scenario");

			await CreateService().DeleteProjectAsync(deleted.Id);

			Assert.True(await DbContext.Projects.AnyAsync(stored => stored.Id == kept.Id));
			Assert.True(await DbContext.DiagramRevisions.AnyAsync(revision => revision.ProjectId == kept.Id));
			Assert.True(await DbContext.SavedScenarios.AnyAsync(scenario => scenario.ProjectId == kept.Id));
		}

		[Fact]
		public async Task DeleteProjectAsync_UnknownId_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() => CreateService().DeleteProjectAsync(Guid.NewGuid()));
		}
	}
}
