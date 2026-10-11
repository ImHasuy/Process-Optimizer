using System.Text;
using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Service;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerLib.Model;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Service
{
	public sealed class DiagramServiceTests : ServiceTestBase
	{
		private const string StaleRevisionMessage = "The diagram was changed by another session. Reload it before saving.";
		private const string TooManyFlowNodesMessage = "The diagram must not contain more than 1000 flow nodes.";
		private const string XmlTooLargeMessage = "The diagram XML must not be larger than 2 MB.";
		private const string NotWellFormedMessage = "The diagram is not well-formed XML or contains a DTD.";
		private const string MalformedXml = "<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"><bpmn:process>";
		private const decimal WorkedExampleMaxCost = 25000m;

		private static readonly TimeSpan SaveDelay = TimeSpan.FromHours(2);

		private readonly DiagramService _service;

		public DiagramServiceTests()
		{
			_service = new DiagramService(UnitOfWork, ProjectAccessGuard, Analysis.RevisionFactory, new ProcessJsonConverter(), TimeProvider);
		}

		[Fact]
		public async Task GetLatestDiagramAsync_NewProject_ReturnsRevisionOneWithDefaultXml()
		{
			var project = await TestData.AddProjectAsync();

			var revision = await _service.GetLatestDiagramAsync(project.Id);

			Assert.Equal(1, revision.RevisionNumber);
			Assert.Equal(DefaultDiagram.Xml, revision.BpmnXml);
			Assert.Equal(ContentHash.Sha256Hex(DefaultDiagram.Xml), revision.ContentSha256);
			Assert.Equal(Encoding.UTF8.GetByteCount(DefaultDiagram.Xml), revision.SizeBytes);
		}

		[Fact]
		public async Task GetLatestDiagramAsync_AfterNewRevision_ReturnsNewestRevision()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var revision = await _service.GetLatestDiagramAsync(project.Id);

			Assert.Equal(2, revision.RevisionNumber);
			Assert.Equal(BpmnFixtures.WorkedExample, revision.BpmnXml);
		}

		[Fact]
		public async Task GetLatestDiagramAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.GetLatestDiagramAsync(projectId));
		}

		[Fact]
		public async Task SaveDiagramAsync_CurrentBaseRevision_AppendsRevisionTwo()
		{
			var project = await TestData.AddProjectAsync();

			var revision = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample, 1));

			Assert.Equal(2, revision.RevisionNumber);
			Assert.Equal(BpmnFixtures.WorkedExample, revision.BpmnXml);
			Assert.Equal(ContentHash.Sha256Hex(BpmnFixtures.WorkedExample), revision.ContentSha256);
			Assert.Equal(Encoding.UTF8.GetByteCount(BpmnFixtures.WorkedExample), revision.SizeBytes);
			Assert.Equal(12, revision.FlowNodeCount);
			Assert.Equal(2, await CountRevisionsAsync(project.Id));
		}

		[Fact]
		public async Task SaveDiagramAsync_IdenticalXml_StillAppendsRevision()
		{
			var project = await TestData.AddProjectAsync();

			var revision = await _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml, 1));

			Assert.Equal(2, revision.RevisionNumber);
			Assert.Equal(ContentHash.Sha256Hex(DefaultDiagram.Xml), revision.ContentSha256);
			Assert.Equal(2, await CountRevisionsAsync(project.Id));
		}

		[Fact]
		public async Task SaveDiagramAsync_ConsecutiveSaves_IncrementRevisionNumber()
		{
			var project = await TestData.AddProjectAsync();
			await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample, 1));

			var revision = await _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml, 2));

			Assert.Equal(3, revision.RevisionNumber);
			Assert.Equal(3, await CountRevisionsAsync(project.Id));
		}

		[Fact]
		public async Task SaveDiagramAsync_StaleBaseRevision_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var exception = await Assert.ThrowsAsync<ConflictException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml, 1)));

			Assert.Equal(StaleRevisionMessage, exception.Message);
		}

		[Fact]
		public async Task SaveDiagramAsync_BaseRevisionAheadOfLatest_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();

			await Assert.ThrowsAsync<ConflictException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml, 2)));
		}

		[Fact]
		public async Task SaveDiagramAsync_StaleBaseRevision_PersistsNothing()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);
			TimeProvider.Advance(SaveDelay);

			await Assert.ThrowsAsync<ConflictException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml, 1)));

			Assert.Equal(2, await CountRevisionsAsync(project.Id));
			var stored = await LoadProjectAsync(project.Id);
			Assert.Equal(2, stored.LatestRevisionNumber);
			Assert.Equal(FixedNow.UtcDateTime, stored.UpdatedAt);
		}

		[Fact]
		public async Task SaveDiagramAsync_ConcurrentInsertOfSameRevisionNumber_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);
			project.LatestRevisionNumber = 1;

			var exception = await Assert.ThrowsAsync<ConflictException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml, 1)));

			Assert.Equal(StaleRevisionMessage, exception.Message);
		}

		[Fact]
		public async Task SaveDiagramAsync_DiagramWithSemanticErrors_IsSavedWithIssueCounts()
		{
			var project = await TestData.AddProjectAsync();

			var revision = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.DisconnectedDiagram(), 1));

			Assert.Equal(2, revision.RevisionNumber);
			Assert.True(revision.ErrorCount > 0);
			Assert.False(revision.IsOptimizable);
		}

		[Fact]
		public async Task SaveDiagramAsync_DiagramWithSemanticErrors_StoresNoLimits()
		{
			var project = await TestData.AddProjectAsync();

			var revision = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.DisconnectedDiagram(), 1));

			var stored = await FindStoredRevisionAsync(project.Id, revision.RevisionNumber);
			Assert.Null(stored.MaxCost);
			Assert.Null(stored.MaxDurationBound);
		}

		[Fact]
		public async Task SaveDiagramAsync_ValidDiagram_ReturnsValidationSummary()
		{
			var project = await TestData.AddProjectAsync();

			var revision = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample, 1));

			Assert.True(revision.IsOptimizable);
			Assert.Equal(0, revision.ErrorCount);
		}

		[Fact]
		public async Task SaveDiagramAsync_OptimizableDiagram_StoresMaxCostAndBound()
		{
			var project = await TestData.AddProjectAsync();

			var revision = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample, 1));

			var stored = await FindStoredRevisionAsync(project.Id, revision.RevisionNumber);
			Assert.Equal(WorkedExampleMaxCost, stored.MaxCost);
			Assert.NotNull(stored.MaxDurationBound);
			Assert.True(stored.MaxDurationBound > 0);
		}

		[Fact]
		public async Task SaveDiagramAsync_MalformedXml_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(MalformedXml, 1)));

			Assert.Equal(NotWellFormedMessage, exception.Message);
		}

		[Fact]
		public async Task SaveDiagramAsync_DtdPayload_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.Load("Parsing/xxe.bpmn"), 1)));
		}

		[Fact]
		public async Task SaveDiagramAsync_OversizedXml_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(OversizedXml(), 1)));

			Assert.Equal(XmlTooLargeMessage, exception.Message);
		}

		[Fact]
		public async Task SaveDiagramAsync_TooManyFlowNodes_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnDiagramBuilder.Chain(AnalysisLimits.MaxFlowNodes), 1)));

			Assert.Equal(TooManyFlowNodesMessage, exception.Message);
		}

		[Fact]
		public async Task SaveDiagramAsync_InvalidXml_PersistsNothing()
		{
			var project = await TestData.AddProjectAsync();

			await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(MalformedXml, 1)));

			Assert.Equal(1, await CountRevisionsAsync(project.Id));
			Assert.Equal(1, (await LoadProjectAsync(project.Id)).LatestRevisionNumber);
		}

		[Fact]
		public async Task SaveDiagramAsync_InvalidXmlWithStaleBaseRevision_ThrowsInvalidRequestBeforeConflict()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(MalformedXml, 1)));
		}

		[Fact]
		public async Task SaveDiagramAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.SaveDiagramAsync(projectId, SaveRequest(MalformedXml, 1)));
		}

		[Fact]
		public async Task SaveDiagramAsync_ValidDiagram_UpdatesLatestRevisionNumberAndTimestamp()
		{
			var project = await TestData.AddProjectAsync();
			TimeProvider.Advance(SaveDelay);

			var revision = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample, 1));

			var expectedTime = FixedNow.Add(SaveDelay).UtcDateTime;
			var stored = await LoadProjectAsync(project.Id);
			Assert.Equal(2, stored.LatestRevisionNumber);
			Assert.Equal(expectedTime, stored.UpdatedAt);
			Assert.Equal(FixedNow.UtcDateTime, stored.CreatedAt);
			Assert.Equal(expectedTime, revision.CreatedAt);
		}

		[Fact]
		public async Task SaveDiagramAsync_FiftiethRevision_PrunesNothing()
		{
			var project = await TestData.AddProjectAsync();
			await AddRevisionsUpToAsync(project, AnalysisLimits.RevisionsToKeep - 1);

			await _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml, AnalysisLimits.RevisionsToKeep - 1));

			Assert.Equal(AnalysisLimits.RevisionsToKeep, await CountRevisionsAsync(project.Id));
			Assert.True(await RevisionExistsAsync(project.Id, 1));
		}

		[Fact]
		public async Task SaveDiagramAsync_FiftyFirstRevision_PrunesOldestUnreferencedRevision()
		{
			var project = await TestData.AddProjectAsync();
			await AddRevisionsUpToAsync(project, AnalysisLimits.RevisionsToKeep);

			await _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml, AnalysisLimits.RevisionsToKeep));

			Assert.Equal(AnalysisLimits.RevisionsToKeep, await CountRevisionsAsync(project.Id));
			Assert.False(await RevisionExistsAsync(project.Id, 1));
			Assert.True(await RevisionExistsAsync(project.Id, 2));
			Assert.True(await RevisionExistsAsync(project.Id, AnalysisLimits.RevisionsToKeep + 1));
		}

		[Fact]
		public async Task SaveDiagramAsync_RevisionReferencedBySavedScenario_IsKept()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddSavedScenarioAsync(project, 1, $"Scenario {Guid.NewGuid():N}");
			await AddRevisionsUpToAsync(project, AnalysisLimits.RevisionsToKeep);

			await _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml, AnalysisLimits.RevisionsToKeep));

			Assert.Equal(AnalysisLimits.RevisionsToKeep + 1, await CountRevisionsAsync(project.Id));
			Assert.True(await RevisionExistsAsync(project.Id, 1));
		}

		[Fact]
		public async Task SaveDiagramAsync_SavedScenarioOfOtherProject_DoesNotProtectRevision()
		{
			var otherProject = await TestData.AddProjectAsync();
			await TestData.AddSavedScenarioAsync(otherProject, 1, $"Scenario {Guid.NewGuid():N}");
			var project = await TestData.AddProjectAsync();
			await AddRevisionsUpToAsync(project, AnalysisLimits.RevisionsToKeep);

			await _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml, AnalysisLimits.RevisionsToKeep));

			Assert.False(await RevisionExistsAsync(project.Id, 1));
			Assert.True(await RevisionExistsAsync(otherProject.Id, 1));
		}

		[Fact]
		public async Task ListRevisionsAsync_SeveralRevisions_ReturnsNewestFirst()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);
			await TestData.AddRevisionAsync(project, DefaultDiagram.Xml);

			var revisions = await _service.ListRevisionsAsync(project.Id);

			Assert.Equal(new[] { 3, 2, 1 }, revisions.Select(revision => revision.RevisionNumber));
			Assert.Equal(12, revisions[1].FlowNodeCount);
			Assert.True(revisions[1].IsOptimizable);
		}

		[Fact]
		public async Task ListRevisionsAsync_OtherProjectRevisions_AreExcluded()
		{
			var project = await TestData.AddProjectAsync();
			var otherProject = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(otherProject, BpmnFixtures.WorkedExample);

			var revisions = await _service.ListRevisionsAsync(project.Id);

			Assert.Equal(1, Assert.Single(revisions).RevisionNumber);
		}

		[Fact]
		public async Task ListRevisionsAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.ListRevisionsAsync(projectId));
		}

		[Fact]
		public async Task GetRevisionAsync_ExistingNumber_ReturnsThatRevision()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var revision = await _service.GetRevisionAsync(project.Id, 1);

			Assert.Equal(1, revision.RevisionNumber);
			Assert.Equal(DefaultDiagram.Xml, revision.BpmnXml);
		}

		[Fact]
		public async Task GetRevisionAsync_UnknownNumber_ThrowsNotFound()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetRevisionAsync(project.Id, 2));

			Assert.Equal(ErrorMessages.RevisionNotFound, exception.Message);
		}

		[Fact]
		public async Task GetRevisionAsync_RevisionOfOtherProject_ThrowsNotFound()
		{
			var project = await TestData.AddProjectAsync();
			var otherProject = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(otherProject, BpmnFixtures.WorkedExample);

			await Assert.ThrowsAsync<NotFoundException>(() => _service.GetRevisionAsync(project.Id, 2));
		}

		[Fact]
		public async Task GetRevisionAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.GetRevisionAsync(projectId, 1));
		}

		[Fact]
		public async Task GetDiagramFileAsync_ExistingProject_ReturnsLatestXmlAndBpmnFileName()
		{
			var project = await TestData.AddProjectAsync(name: "Roadmap 2026");
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var file = await _service.GetDiagramFileAsync(project.Id);

			Assert.Equal("Roadmap 2026.bpmn", file.FileName);
			Assert.Equal(BpmnFixtures.WorkedExample, file.Content);
		}

		[Fact]
		public async Task GetDiagramFileAsync_NameWithForbiddenCharacters_ReplacesThem()
		{
			var project = await TestData.AddProjectAsync(name: "  Plan/\"A\";B  ");

			var file = await _service.GetDiagramFileAsync(project.Id);

			Assert.Equal("Plan__A__B.bpmn", file.FileName);
		}

		[Fact]
		public async Task GetDiagramFileAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.GetDiagramFileAsync(projectId));
		}

		[Fact]
		public async Task SaveJsonDiagramAsync_CurrentBaseRevision_AppendsConvertedRevision()
		{
			var project = await TestData.AddProjectAsync();

			var revision = await _service.SaveJsonDiagramAsync(project.Id, JsonSaveRequest(1));

			Assert.Equal(2, revision.RevisionNumber);
			Assert.Equal(3, revision.FlowNodeCount);
			Assert.True(revision.IsOptimizable);
			Assert.Equal(0, revision.WarningCount);
			Assert.Contains("po:duration=\"0.010417\"", revision.BpmnXml);
			Assert.Equal(2, await CountRevisionsAsync(project.Id));
		}

		[Fact]
		public async Task SaveJsonDiagramAsync_ProjectInHoursAndUsd_ConvertsWithProjectSettings()
		{
			var project = await TestData.AddProjectAsync(timeUnit: ETimeUnit.Hours, currency: "USD");
			var request = JsonSaveRequest(1);
			request.States![1].Price = "$12.50";

			var revision = await _service.SaveJsonDiagramAsync(project.Id, request);

			Assert.Contains("po:duration=\"0.25\"", revision.BpmnXml);
			Assert.Contains("po:cost=\"12.5\"", revision.BpmnXml);
			Assert.Equal(0, revision.ErrorCount);
		}

		[Fact]
		public async Task SaveJsonDiagramAsync_StaleBaseRevision_ThrowsConflictAndPersistsNothing()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var exception = await Assert.ThrowsAsync<ConflictException>(() => _service.SaveJsonDiagramAsync(project.Id, JsonSaveRequest(1)));

			Assert.Equal(StaleRevisionMessage, exception.Message);
			Assert.Equal(2, await CountRevisionsAsync(project.Id));
		}

		[Fact]
		public async Task SaveJsonDiagramAsync_InvalidIdWithStaleBaseRevision_ThrowsInvalidRequestBeforeConflict()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);
			var request = JsonSaveRequest(1);
			request.States![0].Id = "1 bad";

			await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveJsonDiagramAsync(project.Id, request));

			Assert.Equal(2, await CountRevisionsAsync(project.Id));
		}

		[Fact]
		public async Task SaveJsonDiagramAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.SaveJsonDiagramAsync(projectId, JsonSaveRequest(1)));
		}

		private static DiagramJsonRevisionCreateDTO JsonSaveRequest(int baseRevision) =>
			new() { States = ProcessJsonFixtures.SampleStates(), Branches = ProcessJsonFixtures.SampleBranches(), BaseRevision = baseRevision };

		private static DiagramRevisionCreateDTO SaveRequest(string bpmnXml, int baseRevision) =>
			new() { BpmnXml = bpmnXml, BaseRevision = baseRevision };

		private static string OversizedXml() =>
			DefaultDiagram.Xml + $"<!--{new string('x', AnalysisLimits.MaxXmlBytes)}-->";

		private async Task AddRevisionsUpToAsync(Project project, int latestRevisionNumber)
		{
			while (project.LatestRevisionNumber < latestRevisionNumber)
			{
				await TestData.AddRevisionAsync(project, DefaultDiagram.Xml);
			}
		}

		private Task<int> CountRevisionsAsync(Guid projectId) =>
			DbContext.DiagramRevisions.CountAsync(revision => revision.ProjectId == projectId);

		private Task<bool> RevisionExistsAsync(Guid projectId, int revisionNumber) =>
			DbContext.DiagramRevisions.AnyAsync(revision => revision.ProjectId == projectId && revision.RevisionNumber == revisionNumber);

		private Task<DiagramRevision> FindStoredRevisionAsync(Guid projectId, int revisionNumber) =>
			DbContext.DiagramRevisions.AsNoTracking().SingleAsync(revision => revision.ProjectId == projectId && revision.RevisionNumber == revisionNumber);
	}
}
