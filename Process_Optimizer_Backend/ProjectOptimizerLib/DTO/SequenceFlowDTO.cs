namespace ProjectOptimizerLib.DTO
{
	public sealed class SequenceFlowDTO
	{
		public string BpmnId { get; init; } = string.Empty;
		public string SourceId { get; init; } = string.Empty;
		public string TargetId { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public string? ConditionExpression { get; init; }
		public bool IsDefault { get; init; }
	}
}
