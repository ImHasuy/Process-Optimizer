using Mapster;
using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Repository
{
	public sealed class ProjectRepository(ProjectOptimizerDbContext dbContext) : Repository<Project>(dbContext), IProjectRepository
	{
		public Task<List<ProjectListItemDTO>> ListAsync() =>
			Entities.AsNoTracking()
				.OrderByDescending(project => project.UpdatedAt)
				.ThenBy(project => project.Id)
				.ProjectToType<ProjectListItemDTO>()
				.ToListAsync();

		public Task<bool> IsNameTakenAsync(string normalizedName, Guid? excludedProjectId) =>
			Entities.AnyAsync(project => project.NormalizedName == normalizedName && project.Id != excludedProjectId);
	}
}
