using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IScenarioService
	{
		Task<ScenarioCountDTO> GetScenarioCountAsync(Guid projectId);
		Task<List<ScenarioDTO>> GetAllScenariosAsync(Guid projectId);
		Task<ScenarioDTO> GetScenarioAsync(Guid projectId, long index);
		Task<ResourceLimitsDTO> GetResourceLimitsAsync(Guid projectId, ResourceLimitsQueryDTO query);
	}
}
