using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Helper
{
	public static class ProjectFactory
	{
		public const string DefaultCurrency = "EUR";
		public const ETimeUnit DefaultTimeUnit = ETimeUnit.Days;

		public static Project Create(string name, string? description, ETimeUnit timeUnit, string currency, DateTime now) => new()
		{
			Id = Guid.NewGuid(),
			Name = name,
			NormalizedName = NameNormalizer.Normalize(name),
			Description = description,
			TimeUnit = timeUnit,
			Currency = currency,
			CreatedAt = now,
			UpdatedAt = now
		};

		public static void AddRevisions(Project project, IDiagramRevisionFactory revisionFactory, params string[] bpmnXmls)
		{
			foreach (var bpmnXml in bpmnXmls)
			{
				project.LatestRevisionNumber++;
				project.Revisions.Add(revisionFactory.Create(project.Id, project.LatestRevisionNumber, bpmnXml, project.CreatedAt));
			}
		}
	}
}
