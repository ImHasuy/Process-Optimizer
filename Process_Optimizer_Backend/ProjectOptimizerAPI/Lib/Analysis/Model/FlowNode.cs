using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record FlowNode(
		string BpmnId,
		string Name,
		EFlowNodeKind Kind,
		string BpmnType,
		decimal Duration,
		decimal Cost,
		bool Parallelizable,
		bool HasDuration,
		bool HasCost,
		EGatewayDirection GatewayDirection,
		bool IsControllable,
		IReadOnlyList<string> DependsOn,
		IReadOnlyList<string> Dependents,
		Estimate? Estimate,
		IReadOnlyList<CrashOption> CrashOptions)
	{
		public bool IsActivity => IsActivityKind(Kind);

		public bool IsMissingEstimate => IsActivity && (!HasDuration || !HasCost);

		public static bool IsActivityKind(EFlowNodeKind kind) => kind is EFlowNodeKind.Task or EFlowNodeKind.SubProcess or EFlowNodeKind.CallActivity;

		public bool IsGateway => Kind is EFlowNodeKind.ExclusiveGateway or EFlowNodeKind.ParallelGateway or EFlowNodeKind.InclusiveGateway or EFlowNodeKind.EventBasedGateway;
	}
}
