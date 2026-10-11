using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Service
{
	public sealed class ScenarioCatalogTests : ServiceTestBase
	{
		private const int FiftyFourChoices = 54;

		[Fact]
		public async Task GetIndexedRevisionAsync_WorkedExample_ReturnsCountAndContext()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);

			var result = await ScenarioCatalog.GetIndexedRevisionAsync(project.Id, null);

			Assert.Equal(2, result.Count);
			Assert.Equal(project.Id, result.Revision.Context.ProjectId);
			Assert.Equal(1, result.Revision.Context.RevisionNumber);
		}

		[Fact]
		public async Task GetIndexedRevisionAsync_DiagramWithErrors_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();

			await Assert.ThrowsAsync<ConflictException>(() => ScenarioCatalog.GetIndexedRevisionAsync(project.Id, null));
		}

		[Fact]
		public async Task GetIndexedRevisionAsync_TooManyScenarios_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FiftyFourChoices));

			var exception = await Assert.ThrowsAsync<ConflictException>(() => ScenarioCatalog.GetIndexedRevisionAsync(project.Id, null));

			Assert.Equal(ErrorMessages.TooManyScenarios, exception.Message);
		}

		[Fact]
		public async Task GetIndexedRevisionAsync_UnknownRevision_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);

			await Assert.ThrowsAsync<InvalidRequestException>(() => ScenarioCatalog.GetIndexedRevisionAsync(project.Id, 3));
		}

		[Fact]
		public async Task GetOptimizableRevisionAsync_TooManyScenarios_ReturnsAnalysis()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FiftyFourChoices));

			var result = await ScenarioCatalog.GetOptimizableRevisionAsync(project.Id, null);

			Assert.True(result.Analysis.ScenarioCount > AnalysisLimits.MaxIndexableScenarios);
		}

		[Fact]
		public async Task GetOptimizableRevisionAsync_DiagramWithErrors_ThrowsConflict()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<ConflictException>(() => ScenarioCatalog.GetOptimizableRevisionAsync(project.Id, null));

			Assert.Equal(ErrorMessages.DiagramHasErrors, exception.Message);
		}

		[Fact]
		public async Task GetOptimizableRevisionAsync_UnknownProject_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() => ScenarioCatalog.GetOptimizableRevisionAsync(Guid.NewGuid(), null));
		}

		[Fact]
		public async Task CreateLatestRevisionRankerAsync_WorkedExample_RanksKeys()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);

			var ranker = await ScenarioCatalog.CreateLatestRevisionRankerAsync(project.Id);

			Assert.Equal(0, ranker("G3=E"));
			Assert.Equal(1, ranker("G3=F"));
		}

		[Fact]
		public async Task CreateLatestRevisionRankerAsync_LatestNotOptimizable_ReturnsNullForEveryKey()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnFixtures.WorkedExample);
			await TestData.AddRevisionAsync(project, DefaultDiagram.Xml);

			var ranker = await ScenarioCatalog.CreateLatestRevisionRankerAsync(project.Id);

			Assert.Null(ranker("G3=E"));
			Assert.Null(ranker("G3=F"));
		}

		[Fact]
		public async Task CreateLatestRevisionRankerAsync_LatestTooManyScenarios_ReturnsNull()
		{
			var project = await TestData.AddProjectAsync(bpmnXml: BpmnDiagramBuilder.SequentialExclusiveChoices(FiftyFourChoices));

			var ranker = await ScenarioCatalog.CreateLatestRevisionRankerAsync(project.Id);

			Assert.Null(ranker("Split1=Task1A"));
		}

		[Fact]
		public async Task CreateLatestRevisionRankerAsync_UnknownProject_ThrowsNotFound()
		{
			await Assert.ThrowsAsync<NotFoundException>(() => ScenarioCatalog.CreateLatestRevisionRankerAsync(Guid.NewGuid()));
		}
	}
}
