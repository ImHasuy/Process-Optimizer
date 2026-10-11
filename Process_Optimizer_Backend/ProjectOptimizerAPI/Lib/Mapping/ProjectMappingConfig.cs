using System.Linq.Expressions;
using Mapster;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Mapping
{
	public sealed class ProjectMappingConfig : IRegister
	{
		private static readonly Expression<Func<Project, DiagramRevision>> LatestRevision = source => source.Revisions
			.First(revision => revision.RevisionNumber == source.LatestRevisionNumber);

		public void Register(TypeAdapterConfig config)
		{
			config.NewConfig<Project, ProjectListItemDTO>()
				.Map(destination => destination.IsOptimizable, LatestRevisionValue(revision => revision.IsOptimizable))
				.Map(destination => destination.MaxCost, LatestRevisionValue(revision => revision.MaxCost))
				.Map(destination => destination.MaxDurationBound, LatestRevisionValue(revision => revision.MaxDurationBound));

			config.NewConfig<Project, ProjectDetailsDTO>()
				.Map(destination => destination.LatestRevision, LatestRevision)
				.Map(destination => destination.MaxCost, LatestRevisionValue(revision => revision.MaxCost))
				.Map(destination => destination.MaxDurationBound, LatestRevisionValue(revision => revision.MaxDurationBound));
		}

		private static Expression<Func<Project, TValue?>> LatestRevisionValue<TValue>(Expression<Func<DiagramRevision, TValue?>> selector) =>
			source => source.Revisions.AsQueryable()
				.Where(revision => revision.RevisionNumber == source.LatestRevisionNumber)
				.Select(selector)
				.FirstOrDefault();
	}
}
