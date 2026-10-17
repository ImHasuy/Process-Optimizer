namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record ResolvedDiagram(
		ProjectContext Context,
		string BpmnXml,
		string ContentSha256);
}
