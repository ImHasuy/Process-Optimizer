using Mapster;
using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Repository
{
	public sealed class DiagramRepository(ProjectOptimizerDbContext dbContext) : IDiagramRepository
	{
		public Task<Diagram?> FindAsync(Guid projectId) =>
			dbContext.Diagrams.FirstOrDefaultAsync(diagram => diagram.ProjectId == projectId);

		public Task<DiagramDTO?> FindDtoAsync(Guid projectId) =>
			dbContext.Diagrams.AsNoTracking()
				.Where(diagram => diagram.ProjectId == projectId)
				.ProjectToType<DiagramDTO>()
				.FirstOrDefaultAsync();
	}
}
