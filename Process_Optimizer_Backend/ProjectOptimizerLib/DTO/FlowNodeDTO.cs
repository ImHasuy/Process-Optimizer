using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class FlowNodeDTO
	{
		public string BpmnId { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public EFlowNodeKind Kind { get; init; }
		public string BpmnType { get; init; } = string.Empty;
		public decimal Duration { get; init; }
		public decimal Cost { get; init; }
		public bool Parallelizable { get; init; }
		public EGatewayDirection GatewayDirection { get; init; }
		public bool IsControllable { get; init; }
		public List<string> DependsOn { get; init; } = new();
		public List<string> Dependents { get; init; } = new();
		public EstimateDTO? Estimate { get; init; }
		public List<CrashOptionDTO> CrashOptions { get; init; } = new();
	}
}
