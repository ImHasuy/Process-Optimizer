using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IScenarioService
	{
		Task<ScenarioCountDTO> GetScenarioCountAsync(Guid projectId, int? revision);
		Task<PagedResultDTO<ScenarioListItemDTO>> ListScenariosAsync(Guid projectId, ScenarioFilterDTO filter);
		Task<ScenarioDTO> GetScenarioAsync(Guid projectId, long index, int? revision);
		Task<ResourceLimitsDTO> GetResourceLimitsAsync(Guid projectId, ResourceLimitsQueryDTO query);
	}
}
