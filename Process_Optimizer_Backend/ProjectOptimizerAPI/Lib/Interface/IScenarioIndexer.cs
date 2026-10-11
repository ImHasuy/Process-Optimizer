using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IScenarioIndexer
	{
		ScenarioSelection Unrank(StructureFragment root, long index);
		long? Rank(StructureFragment root, string scenarioKey);
	}
}
