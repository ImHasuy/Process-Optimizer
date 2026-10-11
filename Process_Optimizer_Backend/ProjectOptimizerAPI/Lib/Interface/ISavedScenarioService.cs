using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface ISavedScenarioService
	{
		Task<SavedScenarioDTO> CreateSavedScenarioAsync(Guid projectId, SavedScenarioCreateDTO request);
		Task<List<SavedScenarioListItemDTO>> ListSavedScenariosAsync(Guid projectId);
		Task<SavedScenarioDTO> GetSavedScenarioAsync(Guid projectId, Guid id);
		Task UpdateSavedScenarioAsync(Guid projectId, Guid id, SavedScenarioUpdateDTO request);
		Task DeleteSavedScenarioAsync(Guid projectId, Guid id);
	}
}
