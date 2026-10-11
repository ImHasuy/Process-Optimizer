using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Database
{
	public static class DatabaseSeeder
	{
		public const string WorkedExampleProjectName = "Web shop launch";
		public const string SampleProjectName = "Sample project";

		public static async Task SeedAsync(ProjectOptimizerDbContext dbContext, IDiagramRevisionFactory revisionFactory, TimeProvider timeProvider)
		{
			if (await dbContext.Projects.AnyAsync())
			{
				return;
			}

			var now = timeProvider.UtcNow();

			dbContext.Projects.Add(CreateProject(
				WorkedExampleProjectName,
				"Worked example of the specification: parallel build, exclusive test resource, choice of deployment target.",
				now,
				revisionFactory,
				DefaultDiagram.Xml,
				SeedDiagrams.WorkedExampleXml));
			dbContext.Projects.Add(CreateProject(
				SampleProjectName,
				"A new project that only has the default diagram.",
				now,
				revisionFactory,
				DefaultDiagram.Xml));
			await dbContext.SaveChangesAsync();
		}

		private static Project CreateProject(string name, string description, DateTime now, IDiagramRevisionFactory revisionFactory, params string[] revisionXmls)
		{
			var project = ProjectFactory.Create(name, description, ProjectFactory.DefaultTimeUnit, ProjectFactory.DefaultCurrency, now);
			ProjectFactory.AddRevisions(project, revisionFactory, revisionXmls);
			return project;
		}
	}
}
