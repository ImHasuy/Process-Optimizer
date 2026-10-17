using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IProjectService
	{
		Task<List<ProjectListItemDTO>> ListProjectsAsync();
		Task<ProjectDetailsDTO> GetProjectAsync(Guid id);
		Task<ProjectDetailsDTO> CreateProjectAsync(ProjectCreateDTO request);
		Task UpdateProjectAsync(Guid id, ProjectUpdateDTO request);
		Task DeleteProjectAsync(Guid id);
	}
}
