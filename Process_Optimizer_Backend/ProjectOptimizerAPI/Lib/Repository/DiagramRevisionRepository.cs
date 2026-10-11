using Mapster;
using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Repository
{
	public sealed class DiagramRevisionRepository(ProjectOptimizerDbContext dbContext) : Repository<DiagramRevision>(dbContext), IDiagramRevisionRepository
	{
		public Task<DiagramRevisionDTO?> FindDtoAsync(Guid projectId, int revisionNumber) =>
			ForRevision(Entities.AsNoTracking(), projectId, revisionNumber)
				.ProjectToType<DiagramRevisionDTO>()
				.FirstOrDefaultAsync();

		public Task<List<DiagramRevisionListItemDTO>> ListForProjectAsync(Guid projectId) =>
			Entities.AsNoTracking()
				.Where(revision => revision.ProjectId == projectId)
				.OrderByDescending(revision => revision.RevisionNumber)
				.ProjectToType<DiagramRevisionListItemDTO>()
				.ToListAsync();

		public Task<DiagramRevision?> FindByNumberAsync(Guid projectId, int revisionNumber) =>
			ForRevision(Entities.AsNoTracking(), projectId, revisionNumber).FirstOrDefaultAsync();

		public Task<List<DiagramRevision>> ListPrunableAsync(Guid projectId, int maxPrunableRevisionNumber, IReadOnlyCollection<int> keptRevisionNumbers) =>
			Entities
				.Where(revision => revision.ProjectId == projectId && revision.RevisionNumber <= maxPrunableRevisionNumber)
				.Where(revision => !keptRevisionNumbers.Contains(revision.RevisionNumber))
				.OrderBy(revision => revision.RevisionNumber)
				.ToListAsync();

		private static IQueryable<DiagramRevision> ForRevision(IQueryable<DiagramRevision> query, Guid projectId, int revisionNumber) =>
			query.Where(revision => revision.ProjectId == projectId && revision.RevisionNumber == revisionNumber);
	}
}
