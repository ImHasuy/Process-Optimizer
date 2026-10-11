using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IProjectRepository : IRepository<Project>
	{
		Task<PagedResultDTO<ProjectListItemDTO>> ListAsync(ProjectFilterDTO filter, PageRequest paging);
		Task<bool> IsNameTakenAsync(string normalizedName, Guid? excludedProjectId);
		Task<string> GetLatestBpmnXmlAsync(Guid projectId);
	}
}
