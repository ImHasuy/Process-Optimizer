using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record ScenarioChoice(
		string GatewayId,
		EFlowNodeKind GatewayKind,
		bool IsControllable,
		IReadOnlyList<string> ChosenTargetIds);
}
