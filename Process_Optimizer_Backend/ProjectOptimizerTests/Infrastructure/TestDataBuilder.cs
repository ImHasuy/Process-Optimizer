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
	}
}
