using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerTests.Infrastructure
{
	public sealed class TestDataBuilder(ProjectOptimizerDbContext dbContext, DateTime now, IDiagramFactory diagramFactory)
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
				CreatedAt = now,
				UpdatedAt = now
			};
			project.Diagram = diagramFactory.Create(project.Id, bpmnXml ?? DefaultDiagram.Xml, now);
			dbContext.Projects.Add(project);
			await dbContext.SaveChangesAsync();
			return project;
		}

		public async Task<Diagram> SetDiagramAsync(Project project, string bpmnXml)
		{
			diagramFactory.Apply(project.Diagram, bpmnXml, now);
			await dbContext.SaveChangesAsync();
			return project.Diagram;
		}

		public async Task<SavedScenario> AddSavedScenarioAsync(Project project, string name, long scenarioIndex = 0, string scenarioKey = "")
		{
			var savedScenario = new SavedScenario
			{
				Id = Guid.NewGuid(),
				ProjectId = project.Id,
				Name = name,
				NormalizedName = NameNormalizer.Normalize(name),
				ScenarioIndex = scenarioIndex,
				OrderingVersion = AnalysisLimits.OrderingVersion,
				ScenarioKey = scenarioKey,
				SnapshotJson = ScenarioSnapshotSerializer.Serialize(CreateSnapshot(project, scenarioIndex, scenarioKey)),
				CreatedAt = now
			};
			dbContext.SavedScenarios.Add(savedScenario);
			await dbContext.SaveChangesAsync();
			return savedScenario;
		}

		private static ScenarioDTO CreateSnapshot(Project project, long scenarioIndex, string scenarioKey) =>
			new()
			{
				ProjectId = project.Id,
				OrderingVersion = AnalysisLimits.OrderingVersion,
				Index = scenarioIndex,
				Count = 1,
				Key = scenarioKey,
				TimeUnit = project.TimeUnit,
				Currency = project.Currency
			};
	}
}
