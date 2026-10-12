using Mapster;
using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Repository
{
	public sealed class SavedScenarioRepository(ProjectOptimizerDbContext dbContext) : Repository<SavedScenario>(dbContext), ISavedScenarioRepository
	{
		public Task<List<SavedScenarioListItemDTO>> ListForProjectAsync(Guid projectId) =>
			OfProject(projectId)
				.AsNoTracking()
				.OrderByDescending(savedScenario => savedScenario.CreatedAt)
				.ThenBy(savedScenario => savedScenario.Id)
				.ProjectToType<SavedScenarioListItemDTO>()
				.ToListAsync();

		public Task<SavedScenario?> FindForProjectAsync(Guid projectId, Guid id) =>
			OfProject(projectId).FirstOrDefaultAsync(savedScenario => savedScenario.Id == id);

		public Task<bool> IsNameTakenAsync(Guid projectId, string normalizedName, Guid? excludedId) =>
			OfProject(projectId).AnyAsync(savedScenario =>
				savedScenario.NormalizedName == normalizedName && (!excludedId.HasValue || savedScenario.Id != excludedId.Value));

		private IQueryable<SavedScenario> OfProject(Guid projectId) =>
			Entities.Where(savedScenario => savedScenario.ProjectId == projectId);
	}
}
