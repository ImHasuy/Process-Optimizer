using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IProjectService
	{
		Task<PagedResultDTO<ProjectListItemDTO>> ListProjectsAsync(ProjectFilterDTO filter);
		Task<ProjectDetailsDTO> GetProjectAsync(Guid id);
		Task<ProjectDetailsDTO> CreateProjectAsync(ProjectCreateDTO request);
		Task UpdateProjectAsync(Guid id, ProjectUpdateDTO request);
		Task DeleteProjectAsync(Guid id);
		Task<ProjectDetailsDTO> CopyProjectAsync(Guid id);
	}
}
