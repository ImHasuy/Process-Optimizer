using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerTests.Infrastructure
{
	public sealed class TestDataBuilder(ProjectOptimizerDbContext dbContext, DateTime now, IDiagramRevisionFactory revisionFactory)
	{
		private const string DefaultCurrency = "EUR";

		public async Task<Project> AddProjectAsync(string? name = null, string? bpmnXml = null, ETimeUnit timeUnit = ETimeUnit.Days, string currency = DefaultCurrency)
		{
			var projectName = name ?? $"Project {Guid.NewGuid():N}";
			var project = new Project
			{
				Id = Guid.NewGuid(),
				Name = projectName,
				NormalizedName = NameNormalizer.Normalize(projectName),
				TimeUnit = timeUnit,
				Currency = currency,
				LatestRevisionNumber = 1,
				CreatedAt = now,
				UpdatedAt = now
			};
			project.Revisions.Add(revisionFactory.Create(project.Id, 1, bpmnXml ?? DefaultDiagram.Xml, now));
			dbContext.Projects.Add(project);
			await dbContext.SaveChangesAsync();
			return project;
		}

		public async Task<DiagramRevision> AddRevisionAsync(Project project, string bpmnXml)
		{
			var revision = revisionFactory.Create(project.Id, project.LatestRevisionNumber + 1, bpmnXml, now);
			dbContext.DiagramRevisions.Add(revision);
			project.LatestRevisionNumber = revision.RevisionNumber;
			await dbContext.SaveChangesAsync();
			return revision;
		}

		public async Task<SavedScenario> AddSavedScenarioAsync(Project project, int revisionNumber, string name, long scenarioIndex = 0, string scenarioKey = "")
		{
			var savedScenario = new SavedScenario
			{
				Id = Guid.NewGuid(),
				ProjectId = project.Id,
				Name = name,
				NormalizedName = NameNormalizer.Normalize(name),
				RevisionNumber = revisionNumber,
				ScenarioIndex = scenarioIndex,
				OrderingVersion = AnalysisLimits.OrderingVersion,
				ScenarioKey = scenarioKey,
				SnapshotJson = ScenarioSnapshotSerializer.Serialize(CreateSnapshot(project, revisionNumber, scenarioIndex, scenarioKey)),
				CreatedAt = now
			};
			dbContext.SavedScenarios.Add(savedScenario);
			await dbContext.SaveChangesAsync();
			return savedScenario;
		}

		private static ScenarioDTO CreateSnapshot(Project project, int revisionNumber, long scenarioIndex, string scenarioKey) =>
			new()
			{
				ProjectId = project.Id,
				RevisionNumber = revisionNumber,
				OrderingVersion = AnalysisLimits.OrderingVersion,
				Index = scenarioIndex,
				Count = 1,
				Key = scenarioKey,
				TimeUnit = project.TimeUnit,
				Currency = project.Currency
			};
	}
}
