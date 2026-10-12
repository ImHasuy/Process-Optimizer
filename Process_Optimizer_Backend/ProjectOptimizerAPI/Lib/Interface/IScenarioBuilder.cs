using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IScenarioBuilder
	{
		ScenarioComputation Build(ProcessGraph graph, ScenarioSelection selection);
		ScenarioDTO ToDto(ScenarioComputation computation, ProjectContext context, long count);
		ScenarioListItemDTO ToListItem(ScenarioComputation computation);
	}
}
