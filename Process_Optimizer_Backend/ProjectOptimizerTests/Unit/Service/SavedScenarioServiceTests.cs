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
	public sealed class SavedScenarioServiceTests : ServiceTestBase
	{
		private const int UnindexableChoiceCount = 54;

		private static readonly string FourScenarioDiagram = BpmnDiagramBuilder.SequentialExclusiveChoices(2);
		private static readonly string ChoiceOfAAndB = SingleChoiceDiagram("TaskA", "TaskB");

		private SavedScenarioService CreateService() =>
			new(UnitOfWork, ProjectAccessGuard, ScenarioCatalog, Analysis.Analyzer, Analysis.ScenarioBuilder, TimeProvider);

		private static SavedScenarioCreateDTO CreateRequest(long scenarioIndex, string name, string? note = null) =>
			new() { ScenarioIndex = scenarioIndex, Name = name, Note = note };

		private static SavedScenarioUpdateDTO UpdateRequest(string name, string? note = null) =>
			new() { Name = name, Note = note };

		private static string SingleChoiceDiagram(params string[] branchTaskIds)
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", EFlowNodeKind.ExclusiveGateway)
				.Gateway("Join", EFlowNodeKind.ExclusiveGateway)
				.EndEvent("End")
				.Flow("FlowStart", "Start", "Split")
				.Flow("FlowEnd", "Join", "End");
			foreach (var (taskId, position) in branchTaskIds.Select((taskId, position) => (taskId, position + 1)))
			{
				builder.Task(taskId, taskId, position, position * 100)
					.Flow($"FlowSplit{taskId}", "Split", taskId)
					.Flow($"FlowJoin{taskId}", taskId, "Join");
			}

			return builder.Build();
		}

		private async Task<SavedScenario> LoadStoredAsync(Guid id) =>
			await DbContext.SavedScenarios.AsNoTracking().SingleAsync(savedScenario => savedScenario.Id == id);

		private async Task<long> IndexOfScenarioThroughTaskAsync(Project project, string taskId)
		{
			var indexed = await ScenarioCatalog.GetIndexedDiagramAsync(project.Id);
			for (long index = 0; index < indexed.Count; index++)
			{
				if (Analysis.Analyzer.GetScenario(indexed.Analysis, index).ElementIds.Contains(taskId))
				{
					return index;
				}
			}

			throw new InvalidOperationException($"No scenario passes through {taskId}.");
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_ValidRequest_StoresSnapshotOfScenario()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);

			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(2, "  Fast path  ", "A note"));

			Assert.Equal("Fast path", saved.Name);
			Assert.Equal("A note", saved.Note);
			Assert.Equal(2, saved.ScenarioIndex);
			Assert.Equal(1, saved.OrderingVersion);
			Assert.Equal(FixedNow.UtcDateTime, saved.CreatedAt);
			Assert.Equal(2, saved.Snapshot.Index);
			Assert.Equal(4, saved.Snapshot.Count);
			Assert.Equal(project.Id, saved.Snapshot.ProjectId);
			Assert.Equal(saved.Snapshot.Key, saved.ScenarioKey);
			Assert.NotEmpty(saved.ScenarioKey);
			Assert.True(saved.IsAvailable);
			Assert.Equal(2, saved.CurrentIndex);
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_ValidRequest_PersistsSummaryColumnsOfSnapshot()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);

			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(3, "Summary"));

			var stored = await LoadStoredAsync(saved.Id);
			Assert.Equal(project.Id, stored.ProjectId);
			Assert.Equal("SUMMARY", stored.NormalizedName);
			Assert.Equal(saved.Snapshot.TotalDuration, stored.TotalDuration);
			Assert.Equal(saved.Snapshot.TotalCost, stored.TotalCost);
			Assert.Equal(saved.Snapshot.Key, stored.ScenarioKey);
			Assert.True(stored.TotalCost > 0);
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_AfterDiagramOverwrite_UsesCurrentDiagram()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			await TestData.SetDiagramAsync(project, FourScenarioDiagram);

			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(3, "Current"));

			Assert.Equal(3, saved.ScenarioIndex);
			Assert.Equal(4, saved.Snapshot.Count);
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_LastValidIndex_Succeeds()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);

			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(3, "Last"));

			Assert.Equal(3, saved.ScenarioIndex);
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_IndexEqualToCount_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);

			await Assert.ThrowsAsync<InvalidRequestException>(() =>
				CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(4, "Out of range")));

			Assert.False(await DbContext.SavedScenarios.AnyAsync());
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_DuplicateNameInOtherCasing_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);
			await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(0, "Cheapest"));

			await Assert.ThrowsAsync<ConflictException>(() =>
				CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(1, "  CHEAPEST ")));

			Assert.Equal(1, await DbContext.SavedScenarios.CountAsync());
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_SameNameInOtherProject_Succeeds()
		{
			var firstProject = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);
			var secondProject = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);
			await CreateService().CreateSavedScenarioAsync(firstProject.Id, CreateRequest(0, "Shared"));

			var saved = await CreateService().CreateSavedScenarioAsync(secondProject.Id, CreateRequest(0, "Shared"));

			Assert.Equal("Shared", saved.Name);
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_SameScenarioUnderTwoNames_Succeeds()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);
			var first = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(1, "First"));

			var second = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(1, "Second"));

			Assert.NotEqual(first.Id, second.Id);
			Assert.Equal(first.ScenarioKey, second.ScenarioKey);
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_DiagramWithErrors_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();

			await Assert.ThrowsAsync<ConflictException>(() =>
				CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(0, "Broken")));

			Assert.False(await DbContext.SavedScenarios.AnyAsync());
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_TooManyScenarios_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(UnindexableChoiceCount));

			var exception = await Assert.ThrowsAsync<ConflictException>(() =>
				CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(0, "Too many")));

			Assert.Equal(ErrorMessages.TooManyScenarios, exception.Message);
			Assert.False(await DbContext.SavedScenarios.AnyAsync());
		}

		[Fact]
		public async Task CreateSavedScenarioAsync_UnknownProject_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() =>
				CreateService().CreateSavedScenarioAsync(Guid.NewGuid(), CreateRequest(0, "Nowhere")));
		}

		[Fact]
		public async Task GetSavedScenarioAsync_CurrentDiagramStillContainsKey_ReportsCurrentIndex()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var indexOfB = await IndexOfScenarioThroughTaskAsync(project, "TaskB");
			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(indexOfB, "Through B"));
			await TestData.SetDiagramAsync(project, SingleChoiceDiagram("TaskC", "TaskB", "TaskA"));
			var currentIndexOfB = await IndexOfScenarioThroughTaskAsync(project, "TaskB");

			var result = await CreateService().GetSavedScenarioAsync(project.Id, saved.Id);

			Assert.True(result.IsAvailable);
			Assert.Equal(currentIndexOfB, result.CurrentIndex);
		}

		[Fact]
		public async Task GetSavedScenarioAsync_ChosenTargetRemovedFromDiagram_ReportsUnavailable()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var indexOfB = await IndexOfScenarioThroughTaskAsync(project, "TaskB");
			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(indexOfB, "Through B"));
			await TestData.SetDiagramAsync(project, SingleChoiceDiagram("TaskA", "TaskC"));

			var result = await CreateService().GetSavedScenarioAsync(project.Id, saved.Id);

			Assert.False(result.IsAvailable);
			Assert.Null(result.CurrentIndex);
		}

		[Fact]
		public async Task GetSavedScenarioAsync_DiagramHasErrors_ReportsUnavailable()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(0, "Before break"));
			await TestData.SetDiagramAsync(project, DefaultDiagram.Xml);

			var result = await CreateService().GetSavedScenarioAsync(project.Id, saved.Id);

			Assert.False(result.IsAvailable);
			Assert.Null(result.CurrentIndex);
		}

		[Fact]
		public async Task GetSavedScenarioAsync_AfterDiagramEdit_SnapshotUnchanged()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(1, "Frozen"));
			await TestData.SetDiagramAsync(project, FourScenarioDiagram);

			var result = await CreateService().GetSavedScenarioAsync(project.Id, saved.Id);

			Assert.Equal(2, result.Snapshot.Count);
			Assert.Equal(saved.Snapshot.Key, result.Snapshot.Key);
			Assert.Equal(saved.Snapshot.ElementIds, result.Snapshot.ElementIds);
			Assert.Equal(saved.Snapshot.TotalDuration, result.Snapshot.TotalDuration);
			Assert.Equal(saved.Snapshot.TotalCost, result.Snapshot.TotalCost);
		}

		[Fact]
		public async Task GetSavedScenarioAsync_UnknownId_ThrowsNotFound()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);

			await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetSavedScenarioAsync(project.Id, Guid.NewGuid()));
		}

		[Fact]
		public async Task GetSavedScenarioAsync_IdOfOtherProject_ThrowsNotFound()
		{
			var owner = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var other = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var saved = await TestData.AddSavedScenarioAsync(owner, "Owned");

			await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetSavedScenarioAsync(other.Id, saved.Id));
		}

		[Fact]
		public async Task GetSavedScenarioAsync_UnknownProject_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetSavedScenarioAsync(Guid.NewGuid(), Guid.NewGuid()));
		}

		[Fact]
		public async Task ListSavedScenariosAsync_SeveralScenarios_ReturnsNewestFirstWithAvailability()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var indexOfA = await IndexOfScenarioThroughTaskAsync(project, "TaskA");
			var indexOfB = await IndexOfScenarioThroughTaskAsync(project, "TaskB");
			var throughA = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(indexOfA, "Through A"));
			TimeProvider.Advance(TimeSpan.FromMinutes(5));
			var throughB = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(indexOfB, "Through B"));
			await TestData.SetDiagramAsync(project, SingleChoiceDiagram("TaskA", "TaskC"));

			var result = await CreateService().ListSavedScenariosAsync(project.Id);

			Assert.Equal([throughB.Id, throughA.Id], result.Select(item => item.Id));
			Assert.False(result[0].IsAvailable);
			Assert.True(result[1].IsAvailable);
			Assert.Equal(throughB.Snapshot.TotalCost, result[0].TotalCost);
			Assert.Equal(throughB.Snapshot.TotalDuration, result[0].TotalDuration);
			Assert.Equal(throughB.ScenarioKey, result[0].ScenarioKey);
		}

		[Fact]
		public async Task ListSavedScenariosAsync_OtherProjectsScenarios_AreExcluded()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var other = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			await TestData.AddSavedScenarioAsync(other, "Elsewhere");

			var result = await CreateService().ListSavedScenariosAsync(project.Id);

			Assert.Empty(result);
		}

		[Fact]
		public async Task ListSavedScenariosAsync_UnknownProject_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() => CreateService().ListSavedScenariosAsync(Guid.NewGuid()));
		}

		[Fact]
		public async Task UpdateSavedScenarioAsync_NewNameAndNote_ChangesOnlyNameAndNote()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);
			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(2, "Original", note: "Old note"));
			var before = await LoadStoredAsync(saved.Id);
			TimeProvider.Advance(TimeSpan.FromHours(1));

			await CreateService().UpdateSavedScenarioAsync(project.Id, saved.Id, UpdateRequest("  Renamed  ", "New note"));

			var after = await LoadStoredAsync(saved.Id);
			Assert.Equal("Renamed", after.Name);
			Assert.Equal("RENAMED", after.NormalizedName);
			Assert.Equal("New note", after.Note);
			Assert.Equal(before.SnapshotJson, after.SnapshotJson);
			Assert.Equal(before.ScenarioKey, after.ScenarioKey);
			Assert.Equal(before.ScenarioIndex, after.ScenarioIndex);
			Assert.Equal(before.TotalCost, after.TotalCost);
			Assert.Equal(before.TotalDuration, after.TotalDuration);
			Assert.Equal(before.CreatedAt, after.CreatedAt);
		}

		[Fact]
		public async Task UpdateSavedScenarioAsync_NullNote_ClearsNote()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);
			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(0, "Noted", note: "Remove me"));

			await CreateService().UpdateSavedScenarioAsync(project.Id, saved.Id, UpdateRequest("Noted"));

			Assert.Null((await LoadStoredAsync(saved.Id)).Note);
		}

		[Fact]
		public async Task UpdateSavedScenarioAsync_NameOfOtherSavedScenario_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);
			await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(0, "Taken"));
			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(1, "Mine"));

			await Assert.ThrowsAsync<ConflictException>(() =>
				CreateService().UpdateSavedScenarioAsync(project.Id, saved.Id, UpdateRequest("taken")));

			Assert.Equal("Mine", (await LoadStoredAsync(saved.Id)).Name);
		}

		[Fact]
		public async Task UpdateSavedScenarioAsync_OwnNameInOtherCasing_DoesNotConflict()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);
			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(0, "Mine"));

			await CreateService().UpdateSavedScenarioAsync(project.Id, saved.Id, UpdateRequest("MINE"));

			Assert.Equal("MINE", (await LoadStoredAsync(saved.Id)).Name);
		}

		[Fact]
		public async Task UpdateSavedScenarioAsync_UnknownId_ThrowsNotFound()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);

			await Assert.ThrowsAsync<NotFoundException>(() =>
				CreateService().UpdateSavedScenarioAsync(project.Id, Guid.NewGuid(), UpdateRequest("Ghost")));
		}

		[Fact]
		public async Task UpdateSavedScenarioAsync_IdOfOtherProject_ThrowsNotFound()
		{
			var owner = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var other = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var saved = await TestData.AddSavedScenarioAsync(owner, "Owned");

			await Assert.ThrowsAsync<NotFoundException>(() =>
				CreateService().UpdateSavedScenarioAsync(other.Id, saved.Id, UpdateRequest("Hijacked")));

			Assert.Equal("Owned", (await LoadStoredAsync(saved.Id)).Name);
		}

		[Fact]
		public async Task DeleteSavedScenarioAsync_Existing_RemovesIt()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);
			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(0, "Doomed"));

			await CreateService().DeleteSavedScenarioAsync(project.Id, saved.Id);

			Assert.False(await DbContext.SavedScenarios.AnyAsync(savedScenario => savedScenario.Id == saved.Id));
		}

		[Fact]
		public async Task DeleteSavedScenarioAsync_Deleted_FreesTheName()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);
			var saved = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(0, "Reusable"));
			await CreateService().DeleteSavedScenarioAsync(project.Id, saved.Id);

			var recreated = await CreateService().CreateSavedScenarioAsync(project.Id, CreateRequest(1, "Reusable"));

			Assert.NotEqual(saved.Id, recreated.Id);
		}

		[Fact]
		public async Task DeleteSavedScenarioAsync_UnknownId_ThrowsNotFound()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: FourScenarioDiagram);

			await Assert.ThrowsAsync<NotFoundException>(() => CreateService().DeleteSavedScenarioAsync(project.Id, Guid.NewGuid()));
		}

		[Fact]
		public async Task DeleteSavedScenarioAsync_IdOfOtherProject_ThrowsNotFoundAndKeepsIt()
		{
			var owner = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var other = await TestData.AddProjectAsync(bpmnXml: ChoiceOfAAndB);
			var saved = await TestData.AddSavedScenarioAsync(owner, "Owned");

			await Assert.ThrowsAsync<NotFoundException>(() => CreateService().DeleteSavedScenarioAsync(other.Id, saved.Id));

			Assert.True(await DbContext.SavedScenarios.AnyAsync(savedScenario => savedScenario.Id == saved.Id));
		}
	}
}
