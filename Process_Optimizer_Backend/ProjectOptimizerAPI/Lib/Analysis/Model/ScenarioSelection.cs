namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record ScenarioSelection(
		long Index,
		string Key,
		IReadOnlyList<ScenarioChoice> Choices);
}
