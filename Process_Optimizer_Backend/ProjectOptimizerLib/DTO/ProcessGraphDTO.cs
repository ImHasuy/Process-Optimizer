namespace ProjectOptimizerLib.DTO
{
	public sealed class ProcessGraphDTO
	{
		public int RevisionNumber { get; init; }
		public string ProcessId { get; init; } = string.Empty;
		public List<FlowNodeDTO> Nodes { get; init; } = new();
		public List<SequenceFlowDTO> Edges { get; init; } = new();
	}
}
