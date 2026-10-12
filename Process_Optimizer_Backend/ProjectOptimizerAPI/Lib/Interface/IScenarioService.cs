using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IScenarioService
	{
		Task<ScenarioCountDTO> GetScenarioCountAsync(Guid projectId);
		Task<PagedResultDTO<ScenarioListItemDTO>> ListScenariosAsync(Guid projectId, ScenarioFilterDTO filter);
		Task<ScenarioDTO> GetScenarioAsync(Guid projectId, long index);
		Task<ResourceLimitsDTO> GetResourceLimitsAsync(Guid projectId, ResourceLimitsQueryDTO query);
	}
}
