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
		private const string CopySuffix = " (copy)";
		private const int MaxNameLength = 100;

		private ProjectService CreateService() => new(UnitOfWork, ProjectAccessGuard, Analysis.RevisionFactory, TimeProvider);

		private static ProjectCreateDTO CreateRequest(string name, string? description = null, ETimeUnit? timeUnit = null, string? currency = null) =>
			new() { Name = name, Description = description, TimeUnit = timeUnit, Currency = currency };

		private static ProjectUpdateDTO UpdateRequest(string name, string? description = null, ETimeUnit timeUnit = ETimeUnit.Days, string currency = "EUR") =>
			new() { Name = name, Description = description, TimeUnit = timeUnit, Currency = currency };

		private static ProjectFilterDTO Filter(EProjectSortBy? sortBy = null, ESortDirection? sortDirection = null, string? search = null, int? page = null, int? pageSize = null) =>
			new() { SortBy = sortBy, SortDirection = sortDirection, Search = search, Page = page, PageSize = pageSize };

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

		private static List<string> NamesOf(PagedResultDTO<ProjectListItemDTO> result) => result.Items.Select(item => item.Name).ToList();

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
		public async Task ListProjectsAsync_NoSort_ReturnsUpdatedAtDescending()
		{
			await CreateProjectsOneHourApartAsync("First", "Second", "Third");

			var result = await CreateService().ListProjectsAsync(Filter());

			Assert.Equal(new[] { "Third", "Second", "First" }, NamesOf(result));
		}

		[Fact]
		public async Task ListProjectsAsync_NoPaging_UsesFirstPageOfTwenty()
		{
			await CreateProjectsOneHourApartAsync("First");

			var result = await CreateService().ListProjectsAsync(Filter());

			Assert.Equal(1, result.Page);
			Assert.Equal(20, result.PageSize);
			Assert.Equal(1, result.TotalCount);
		}

		[Fact]
		public async Task ListProjectsAsync_SearchTerm_FiltersCaseInsensitively()
		{
			await CreateProjectsOneHourApartAsync("Alpha Launch", "Beta", "Gamma LAUNCH");

			var result = await CreateService().ListProjectsAsync(Filter(sortBy: EProjectSortBy.Name, search: " launch "));

			Assert.Equal(new[] { "Alpha Launch", "Gamma LAUNCH" }, NamesOf(result));
			Assert.Equal(2, result.TotalCount);
		}

		[Fact]
		public async Task ListProjectsAsync_SearchWithWildcardCharacter_MatchesLiterally()
		{
			await CreateProjectsOneHourApartAsync("Plan 100%", "Plan 1000");

			var result = await CreateService().ListProjectsAsync(Filter(search: "%"));

			Assert.Equal(new[] { "Plan 100%" }, NamesOf(result));
		}

		[Fact]
		public async Task ListProjectsAsync_SortByNameAscending_OrdersByName()
		{
			await CreateProjectsOneHourApartAsync("bravo", "Charlie", "alpha");

			var result = await CreateService().ListProjectsAsync(Filter(EProjectSortBy.Name, ESortDirection.Asc));

			Assert.Equal(new[] { "alpha", "bravo", "Charlie" }, NamesOf(result));
		}

		[Fact]
		public async Task ListProjectsAsync_SortByWithoutDirection_OrdersAscending()
		{
			await CreateProjectsOneHourApartAsync("First", "Second", "Third");

			var result = await CreateService().ListProjectsAsync(Filter(EProjectSortBy.CreatedAt));

			Assert.Equal(new[] { "First", "Second", "Third" }, NamesOf(result));
		}

		[Fact]
		public async Task ListProjectsAsync_DirectionWithoutSortBy_OrdersByUpdatedAtInThatDirection()
		{
			await CreateProjectsOneHourApartAsync("First", "Second", "Third");

			var result = await CreateService().ListProjectsAsync(Filter(sortDirection: ESortDirection.Asc));

			Assert.Equal(new[] { "First", "Second", "Third" }, NamesOf(result));
		}

		[Fact]
		public async Task ListProjectsAsync_SortByNameDescending_OrdersByNameDescending()
		{
			await CreateProjectsOneHourApartAsync("bravo", "Charlie", "alpha");

			var result = await CreateService().ListProjectsAsync(Filter(EProjectSortBy.Name, ESortDirection.Desc));

			Assert.Equal(new[] { "Charlie", "bravo", "alpha" }, NamesOf(result));
		}

		[Fact]
		public async Task ListProjectsAsync_SecondPage_ReturnsPageAndTotalCount()
		{
			await CreateProjectsOneHourApartAsync("A", "B", "C");

			var result = await CreateService().ListProjectsAsync(Filter(EProjectSortBy.Name, page: 2, pageSize: 2));

			Assert.Equal(new[] { "C" }, NamesOf(result));
			Assert.Equal(2, result.Page);
			Assert.Equal(2, result.PageSize);
			Assert.Equal(3, result.TotalCount);
		}

		[Fact]
		public async Task ListProjectsAsync_PageBeyondLastPage_ReturnsEmptyItemsWithTotalCount()
		{
			await CreateProjectsOneHourApartAsync("A", "B");

			var result = await CreateService().ListProjectsAsync(Filter(page: 5, pageSize: 2));

			Assert.Empty(result.Items);
			Assert.Equal(2, result.TotalCount);
		}

		[Fact]
		public async Task ListProjectsAsync_OptimizableLatestRevision_IncludesStoredLimits()
		{
			var project = await TestData.AddProjectAsync("Worked example", BpmnFixtures.WorkedExample);
			var revision = Assert.Single(await LoadRevisionsAsync(project.Id));

			var item = Assert.Single((await CreateService().ListProjectsAsync(Filter())).Items);

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

			var item = Assert.Single((await CreateService().ListProjectsAsync(Filter())).Items);

			Assert.False(item.IsOptimizable);
			Assert.Null(item.MaxCost);
			Assert.Null(item.MaxDurationBound);
		}

		[Fact]
		public async Task ListProjectsAsync_LaterRevisionOptimizable_UsesLatestRevision()
		{
			var project = await TestData.AddProjectAsync("Evolving");
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var item = Assert.Single((await CreateService().ListProjectsAsync(Filter())).Items);

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

		[Fact]
		public async Task CopyProjectAsync_ExistingProject_CopiesLatestXmlAsRevisionOne()
		{
			var source = await TestData.AddProjectAsync("Launch");
			await TestData.AddRevisionAsync(source, BpmnFixtures.WorkedExample);

			var copy = await CreateService().CopyProjectAsync(source.Id);

			var revision = Assert.Single(await LoadRevisionsAsync(copy.Id));
			Assert.Equal(1, revision.RevisionNumber);
			Assert.Equal(BpmnFixtures.WorkedExample, revision.BpmnXml);
			Assert.Equal(1, copy.LatestRevision.RevisionNumber);
			Assert.True(copy.LatestRevision.IsOptimizable);
			Assert.NotNull(copy.MaxCost);
		}

		[Fact]
		public async Task CopyProjectAsync_ExistingProject_CopiesSettingsWithCopyName()
		{
			var source = await CreateService().CreateProjectAsync(CreateRequest("Launch", "Release plan", ETimeUnit.Hours, "USD"));
			TimeProvider.Advance(TimeSpan.FromHours(2));

			var copy = await CreateService().CopyProjectAsync(source.Id);

			Assert.NotEqual(source.Id, copy.Id);
			Assert.Equal("Launch (copy)", copy.Name);
			Assert.Equal("Release plan", copy.Description);
			Assert.Equal(ETimeUnit.Hours, copy.TimeUnit);
			Assert.Equal("USD", copy.Currency);
			Assert.Equal(FixedNow.UtcDateTime.AddHours(2), copy.CreatedAt);
		}

		[Fact]
		public async Task CopyProjectAsync_ExistingProject_LeavesSourceUnchanged()
		{
			var source = await TestData.AddProjectAsync("Launch");
			await TestData.AddRevisionAsync(source, BpmnFixtures.WorkedExample);

			await CreateService().CopyProjectAsync(source.Id);

			Assert.Equal(2, (await LoadRevisionsAsync(source.Id)).Count);
			Assert.Equal("Launch", (await LoadProjectAsync(source.Id)).Name);
		}

		[Fact]
		public async Task CopyProjectAsync_CopyNameTaken_AppendsNumberTwo()
		{
			var source = await TestData.AddProjectAsync("Launch");
			await TestData.AddProjectAsync("LAUNCH (COPY)");

			var copy = await CreateService().CopyProjectAsync(source.Id);

			Assert.Equal("Launch (copy) 2", copy.Name);
		}

		[Fact]
		public async Task CopyProjectAsync_RepeatedCopies_AppendIncreasingNumbers()
		{
			var source = await TestData.AddProjectAsync("Launch");
			var service = CreateService();

			var names = new List<string>();
			for (var copyIndex = 0; copyIndex < 3; copyIndex++)
			{
				names.Add((await service.CopyProjectAsync(source.Id)).Name);
			}

			Assert.Equal(new[] { "Launch (copy)", "Launch (copy) 2", "Launch (copy) 3" }, names);
		}

		[Fact]
		public async Task CopyProjectAsync_HundredCharacterName_TruncatesBaseToKeepHundredCharacters()
		{
			var longName = new string('x', MaxNameLength);
			var source = await TestData.AddProjectAsync(longName);

			var copy = await CreateService().CopyProjectAsync(source.Id);

			Assert.Equal(MaxNameLength, copy.Name.Length);
			Assert.Equal(longName[..(MaxNameLength - CopySuffix.Length)] + CopySuffix, copy.Name);
		}

		[Fact]
		public async Task CopyProjectAsync_HundredCharacterNameWithTakenCopy_TruncatesForNumberedSuffix()
		{
			var longName = new string('x', MaxNameLength);
			var source = await TestData.AddProjectAsync(longName);
			var service = CreateService();
			await service.CopyProjectAsync(source.Id);

			var second = await service.CopyProjectAsync(source.Id);

			Assert.Equal(MaxNameLength, second.Name.Length);
			Assert.EndsWith(CopySuffix + " 2", second.Name);
		}

		[Fact]
		public async Task CopyProjectAsync_UnknownId_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() => CreateService().CopyProjectAsync(Guid.NewGuid()));

			Assert.Equal(0, await DbContext.Projects.CountAsync());
		}
	}
}
