namespace ProjectOptimizerLib.Enum
{
	public enum EFlowNodeKind
	{
		StartEvent,
		EndEvent,
		Task,
		SubProcess,
		CallActivity,
		IntermediateEvent,
		ExclusiveGateway,
		ParallelGateway,
		InclusiveGateway,
		EventBasedGateway
	}
}
