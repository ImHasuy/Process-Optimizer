namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record SequenceFlowEdge(
		string BpmnId,
		string SourceId,
		string TargetId,
		string Name,
		string? ConditionExpression,
		bool IsDefault);
}
