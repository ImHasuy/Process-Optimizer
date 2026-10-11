using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ScenarioChoiceDTO
	{
		public string GatewayId { get; init; } = string.Empty;
		public EFlowNodeKind GatewayKind { get; init; }
		public bool IsControllable { get; init; }
		public List<string> ChosenTargetIds { get; init; } = new();
	}
}
