using System.Text.Json;
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
	public sealed class ScenarioServiceTests : ServiceTestBase
	{
		private const int FiftyFourChoices = 54;
		private const int FourteenChoices = 14;

		private static readonly string[] Scenario0CriticalPath = ["S", "A", "G1", "B", "G2", "D", "G3", "E", "G4", "End"];
		private static readonly string[] Scenario1CriticalPath = ["S", "A", "G1", "B", "G2", "D", "G3", "F", "G4", "End"];
		private static readonly string[] Scenario1ElementIds = ["S", "A", "G1", "B", "C", "G2", "D", "G3", "F", "G4", "End"];

		private readonly ScenarioService _service;

		public ScenarioServiceTests()
		{
			_service = new ScenarioService(ScenarioCatalog, Analysis.Analyzer, Analysis.ScenarioBuilder, Analysis.ResourceLimitCalculator);
		}

		[Fact]
		public async Task GetScenarioCountAsync_WorkedExample_ReturnsTwo()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.GetScenarioCountAsync(project.Id);

			Assert.Equal(2, result.Count);
			Assert.Equal(AnalysisLimits.OrderingVersion, result.OrderingVersion);
		}

		[Fact]
		public async Task GetScenarioCountAsync_AfterDiagramOverwrite_ReturnsCountOfCurrentDiagram()
		{
			var project = await AddWorkedExampleProjectAsync();
			await TestData.SetDiagramAsync(project, BpmnDiagramBuilder.SequentialExclusiveChoices(3));

			var result = await _service.GetScenarioCountAsync(project.Id);

			Assert.Equal(8, result.Count);
		}

		[Fact]
		public async Task GetScenarioCountAsync_DiagramWithErrors_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<ConflictException>(() => _service.GetScenarioCountAsync(project.Id));

			Assert.Equal(ErrorMessages.DiagramHasErrors, exception.Message);
		}

		[Fact]
		public async Task GetScenarioCountAsync_FiftyFourBinaryChoices_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FiftyFourChoices));

			var exception = await Assert.ThrowsAsync<ConflictException>(() => _service.GetScenarioCountAsync(project.Id));

			Assert.Equal(ErrorMessages.TooManyScenarios, exception.Message);
		}

		[Fact]
		public async Task GetScenarioCountAsync_FiftyThreeBinaryChoicesJustAboveLimit_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(53));

			var exception = await Assert.ThrowsAsync<ConflictException>(() => _service.GetScenarioCountAsync(project.Id));

			Assert.Equal(ErrorMessages.TooManyScenarios, exception.Message);
		}

		[Fact]
		public async Task GetScenarioCountAsync_FiftyTwoBinaryChoicesBelowLimit_ReturnsCount()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(52));

			var result = await _service.GetScenarioCountAsync(project.Id);

			Assert.Equal(1L << 52, result.Count);
		}

		[Fact]
		public async Task GetScenarioCountAsync_UnknownProject_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() => _service.GetScenarioCountAsync(Guid.NewGuid()));
		}

		[Fact]
		public async Task GetScenarioAsync_IndexZero_ReturnsG3EqualsEWith21DaysAnd23000()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.GetScenarioAsync(project.Id, 0);

			Assert.Equal(0, result.Index);
			Assert.Equal("G3=E", result.Key);
			Assert.Equal(21m, result.TotalDuration);
			Assert.Equal(23000m, result.TotalCost);
			Assert.Equal(29m, result.SequentialDuration);
			Assert.Equal(2, result.PathCount);
			Assert.Equal(Scenario0CriticalPath, result.CriticalPath);
		}

		[Fact]
		public async Task GetScenarioAsync_IndexOne_ReturnsG3EqualsFWithGoldenTotals()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.GetScenarioAsync(project.Id, 1);

			Assert.Equal("G3=F", result.Key);
			Assert.Equal(20m, result.TotalDuration);
			Assert.Equal(28m, result.SequentialDuration);
			Assert.Equal(25000m, result.TotalCost);
			Assert.Equal(2, result.PathCount);
			Assert.Equal(Scenario1ElementIds, result.ElementIds);
			Assert.Equal(Scenario1CriticalPath, result.CriticalPath);
		}

		[Fact]
		public async Task GetScenarioAsync_IndexOne_ReturnsGoldenScheduleOfActivityC()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.GetScenarioAsync(project.Id, 1);

			var activity = Assert.Single(result.Activities, item => item.BpmnId == "C");
			Assert.Equal(5m, activity.EarliestStart);
			Assert.Equal(13m, activity.EarliestFinish);
			Assert.Equal(7m, activity.LatestStart);
			Assert.Equal(15m, activity.LatestFinish);
			Assert.Equal(2m, activity.Slack);
			Assert.False(activity.IsCritical);
		}

		[Fact]
		public async Task GetScenarioAsync_ValidIndex_ReturnsProjectContext()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.GetScenarioAsync(project.Id, 1);

			Assert.Equal(project.Id, result.ProjectId);
			Assert.Equal(AnalysisLimits.OrderingVersion, result.OrderingVersion);
			Assert.Equal(2, result.Count);
			Assert.Equal(ETimeUnit.Days, result.TimeUnit);
			Assert.Equal("EUR", result.Currency);
		}

		[Fact]
		public async Task GetScenarioAsync_IndexEqualToCount_ThrowsNotFound()
		{
			var project = await AddWorkedExampleProjectAsync();

			var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetScenarioAsync(project.Id, 2));

			Assert.Equal(ErrorMessages.ScenarioNotFound, exception.Message);
		}

		[Fact]
		public async Task GetScenarioAsync_MaximumIndex_ThrowsNotFound()
		{
			var project = await AddWorkedExampleProjectAsync();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.GetScenarioAsync(project.Id, long.MaxValue));
		}

		[Fact]
		public async Task GetScenarioAsync_SameIndexTwice_SerialisesIdentically()
		{
			var project = await AddWorkedExampleProjectAsync();

			var first = await _service.GetScenarioAsync(project.Id, 1);
			var second = await _service.GetScenarioAsync(project.Id, 1);

			Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
		}

		[Fact]
		public async Task GetScenarioAsync_ProjectCopyWithSameXml_ReturnsOwnProjectId()
		{
			var original = await AddWorkedExampleProjectAsync();
			var copy = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample, currency: "USD");
			await _service.GetScenarioAsync(original.Id, 0);

			var result = await _service.GetScenarioAsync(copy.Id, 0);

			Assert.Equal(copy.Id, result.ProjectId);
			Assert.Equal("USD", result.Currency);
		}

		[Fact]
		public async Task GetScenarioAsync_DiagramWithErrors_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();

			await Assert.ThrowsAsync<ConflictException>(() => _service.GetScenarioAsync(project.Id, 0));
		}

		[Fact]
		public async Task GetScenarioAsync_TooManyScenarios_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FiftyFourChoices));

			await Assert.ThrowsAsync<ConflictException>(() => _service.GetScenarioAsync(project.Id, 0));
		}

		[Fact]
		public async Task ListScenariosAsync_DefaultFilter_ReturnsCanonicalOrder()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.ListScenariosAsync(project.Id, new ScenarioFilterDTO());

			Assert.Equal([0L, 1L], result.Items.Select(item => item.Index));
			Assert.Equal(["G3=E", "G3=F"], result.Items.Select(item => item.Key));
			Assert.Equal(1, result.Page);
			Assert.Equal(20, result.PageSize);
			Assert.Equal(2, result.TotalCount);
		}

		[Fact]
		public async Task ListScenariosAsync_DefaultFilter_ReturnsGoldenTotals()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.ListScenariosAsync(project.Id, new ScenarioFilterDTO());

			Assert.Equal([21m, 20m], result.Items.Select(item => item.TotalDuration));
			Assert.Equal([23000m, 25000m], result.Items.Select(item => item.TotalCost));
		}

		[Fact]
		public async Task ListScenariosAsync_SecondPageOfSizeOne_ReturnsIndexOne()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.ListScenariosAsync(project.Id, new ScenarioFilterDTO { Page = 2, PageSize = 1 });

			Assert.Equal(1, Assert.Single(result.Items).Index);
			Assert.Equal(2, result.Page);
			Assert.Equal(1, result.PageSize);
			Assert.Equal(2, result.TotalCount);
		}

		[Fact]
		public async Task ListScenariosAsync_PageBeyondCount_ReturnsEmptyItemsWithTotalCount()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.ListScenariosAsync(project.Id, new ScenarioFilterDTO { Page = 2 });

			Assert.Empty(result.Items);
			Assert.Equal(2, result.TotalCount);
		}

		[Fact]
		public async Task ListScenariosAsync_IndexOrderAboveTenThousandScenarios_ReturnsFirstPage()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FourteenChoices));

			var result = await _service.ListScenariosAsync(project.Id, new ScenarioFilterDTO { PageSize = 3 });

			Assert.Equal([0L, 1L, 2L], result.Items.Select(item => item.Index));
			Assert.Equal(1L << FourteenChoices, result.TotalCount);
		}

		[Fact]
		public async Task ListScenariosAsync_DiagramWithErrors_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();

			await Assert.ThrowsAsync<ConflictException>(() => _service.ListScenariosAsync(project.Id, new ScenarioFilterDTO()));
		}

		[Fact]
		public async Task ListScenariosAsync_TooManyScenarios_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FiftyFourChoices));

			await Assert.ThrowsAsync<ConflictException>(() => _service.ListScenariosAsync(project.Id, new ScenarioFilterDTO()));
		}

		[Fact]
		public async Task GetResourceLimitsAsync_WorkedExample_ReturnsGoldenLimits()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.GetResourceLimitsAsync(project.Id, new ResourceLimitsQueryDTO());

			Assert.Equal(25000m, result.MaxCost);
			ScenarioAssertions.AssertReference(result.MaxCostScenario, 1, "G3=F");
			Assert.Equal(23000m, result.MinCost);
			ScenarioAssertions.AssertReference(result.MinCostScenario, 0, "G3=E");
			Assert.Equal(21m, result.MaxCriticalPathDuration);
			ScenarioAssertions.AssertReference(result.MaxCriticalPathScenario, 0, "G3=E");
			Assert.Equal(20m, result.MinCriticalPathDuration);
			ScenarioAssertions.AssertReference(result.MinCriticalPathScenario, 1, "G3=F");
			Assert.Equal(4m, result.MaxExclusiveDuration);
			Assert.Equal(25m, result.MaxDurationBound);
			Assert.Equal(29m, result.MaxSequentialDuration);
			ScenarioAssertions.AssertReference(result.MaxSequentialScenario, 0, "G3=E");
			Assert.False(result.IsJointWorstCase);
		}

		[Fact]
		public async Task GetResourceLimitsAsync_WorkedExample_ReturnsProjectContextAndPlannedBasis()
		{
			var project = await AddWorkedExampleProjectAsync();

			var result = await _service.GetResourceLimitsAsync(project.Id, new ResourceLimitsQueryDTO());

			Assert.Equal(project.Id, result.ProjectId);
			Assert.Equal(EResourceLimitBasis.Planned, result.Basis);
			Assert.Equal(2, result.ScenarioCount);
			Assert.Equal("EUR", result.Currency);
		}

		[Fact]
		public async Task GetResourceLimitsAsync_FiftyFourBinaryChoices_ReturnsLimitsWithoutReferences()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FiftyFourChoices));

			var result = await _service.GetResourceLimitsAsync(project.Id, new ResourceLimitsQueryDTO());

			Assert.Null(result.ScenarioCount);
			Assert.Null(result.MaxCostScenario);
			Assert.Null(result.MinCostScenario);
			Assert.Null(result.MaxCriticalPathScenario);
			Assert.Null(result.MinCriticalPathScenario);
			Assert.Null(result.MaxSequentialScenario);
			Assert.Equal(FiftyFourChoices * 100m, result.MaxCost);
		}

		[Fact]
		public async Task GetResourceLimitsAsync_DiagramWithErrors_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<ConflictException>(
				() => _service.GetResourceLimitsAsync(project.Id, new ResourceLimitsQueryDTO()));

			Assert.Equal(ErrorMessages.DiagramHasErrors, exception.Message);
		}

		[Fact]
		public async Task GetResourceLimitsAsync_PessimisticBasis_ReportsBasis()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: WorkedExampleWithPessimisticBackend());

			var result = await _service.GetResourceLimitsAsync(project.Id, new ResourceLimitsQueryDTO { Basis = EResourceLimitBasis.Pessimistic });

			Assert.Equal(EResourceLimitBasis.Pessimistic, result.Basis);
			Assert.Equal(25m, result.MaxCriticalPathDuration);
		}

		[Fact]
		public async Task GetResourceLimitsAsync_PlannedBasisWithEstimate_UsesPlannedDuration()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: WorkedExampleWithPessimisticBackend());

			var result = await _service.GetResourceLimitsAsync(project.Id, new ResourceLimitsQueryDTO { Basis = EResourceLimitBasis.Planned });

			Assert.Equal(21m, result.MaxCriticalPathDuration);
		}

		[Fact]
		public async Task GetResourceLimitsAsync_UnknownProject_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(
				() => _service.GetResourceLimitsAsync(Guid.NewGuid(), new ResourceLimitsQueryDTO()));
		}

		private Task<Project> AddWorkedExampleProjectAsync() => TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);

		private static string WorkedExampleWithPessimisticBackend() =>
			BpmnFixtures.WorkedExample.Replace(
				"<bpmn:incoming>F3</bpmn:incoming>",
				"<bpmn:extensionElements><po:estimate optimistic=\"8\" mostLikely=\"10\" pessimistic=\"14\" /></bpmn:extensionElements><bpmn:incoming>F3</bpmn:incoming>");
	}
}
