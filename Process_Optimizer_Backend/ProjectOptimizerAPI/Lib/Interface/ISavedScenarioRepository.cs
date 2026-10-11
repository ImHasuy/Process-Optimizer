using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface ISavedScenarioRepository : IRepository<SavedScenario>
	{
		Task<List<SavedScenarioListItemDTO>> ListForProjectAsync(Guid projectId);
		Task<List<int>> ListReferencedRevisionNumbersAsync(Guid projectId);
		Task<SavedScenario?> FindForProjectAsync(Guid projectId, Guid id);
		Task<bool> IsNameTakenAsync(Guid projectId, string normalizedName, Guid? excludedId);
	}
}
