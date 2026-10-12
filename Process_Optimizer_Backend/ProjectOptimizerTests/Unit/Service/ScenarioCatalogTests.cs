using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Service
{
	public sealed class ScenarioCatalogTests : ServiceTestBase
	{
		private const int FiftyFourChoices = 54;

		[Fact]
		public async Task GetIndexedDiagramAsync_WorkedExample_ReturnsCountAndContext()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);

			var result = await ScenarioCatalog.GetIndexedDiagramAsync(project.Id);

			Assert.Equal(2, result.Count);
			Assert.Equal(project.Id, result.Diagram.Context.ProjectId);
			Assert.Equal(ETimeUnit.Days, result.Diagram.Context.TimeUnit);
		}

		[Fact]
		public async Task GetIndexedDiagramAsync_DiagramWithErrors_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();

			await Assert.ThrowsAsync<ConflictException>(() => ScenarioCatalog.GetIndexedDiagramAsync(project.Id));
		}

		[Fact]
		public async Task GetIndexedDiagramAsync_TooManyScenarios_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FiftyFourChoices));

			var exception = await Assert.ThrowsAsync<ConflictException>(() => ScenarioCatalog.GetIndexedDiagramAsync(project.Id));

			Assert.Equal(ErrorMessages.TooManyScenarios, exception.Message);
		}

		[Fact]
		public async Task GetOptimizableDiagramAsync_TooManyScenarios_ReturnsAnalysis()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FiftyFourChoices));

			var result = await ScenarioCatalog.GetOptimizableDiagramAsync(project.Id);

			Assert.True(result.Analysis.ScenarioCount > AnalysisLimits.MaxIndexableScenarios);
		}

		[Fact]
		public async Task GetOptimizableDiagramAsync_DiagramWithErrors_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<ConflictException>(() => ScenarioCatalog.GetOptimizableDiagramAsync(project.Id));

			Assert.Equal(ErrorMessages.DiagramHasErrors, exception.Message);
		}

		[Fact]
		public async Task GetOptimizableDiagramAsync_UnknownProject_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() => ScenarioCatalog.GetOptimizableDiagramAsync(Guid.NewGuid()));
		}

		[Fact]
		public async Task CreateRankerAsync_WorkedExample_RanksKeys()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);

			var ranker = await ScenarioCatalog.CreateRankerAsync(project.Id);

			Assert.Equal(0, ranker("G3=E"));
			Assert.Equal(1, ranker("G3=F"));
		}

		[Fact]
		public async Task CreateRankerAsync_DiagramNotOptimizable_ReturnsNullForEveryKey()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);
			await TestData.SetDiagramAsync(project, DefaultDiagram.Xml);

			var ranker = await ScenarioCatalog.CreateRankerAsync(project.Id);

			Assert.Null(ranker("G3=E"));
			Assert.Null(ranker("G3=F"));
		}

		[Fact]
		public async Task CreateRankerAsync_TooManyScenarios_ReturnsNull()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FiftyFourChoices));

			var ranker = await ScenarioCatalog.CreateRankerAsync(project.Id);

			Assert.Null(ranker("Split1=Task1A"));
		}

		[Fact]
		public async Task CreateRankerAsync_UnknownProject_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() => ScenarioCatalog.CreateRankerAsync(Guid.NewGuid()));
		}
	}
}
