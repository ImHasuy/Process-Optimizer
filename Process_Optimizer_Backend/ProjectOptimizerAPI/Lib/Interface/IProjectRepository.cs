using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IProjectRepository : IRepository<Project>
	{
		Task<List<ProjectListItemDTO>> ListAsync();
		Task<bool> IsNameTakenAsync(string normalizedName, Guid? excludedProjectId);
	}
}
