using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IScenarioBuilder
	{
		ScenarioComputation Build(ProcessGraph graph, ScenarioSelection selection);
		ScenarioDTO ToDto(ScenarioComputation computation, RevisionContext context, long count);
		ScenarioListItemDTO ToListItem(ScenarioComputation computation);
	}
}
