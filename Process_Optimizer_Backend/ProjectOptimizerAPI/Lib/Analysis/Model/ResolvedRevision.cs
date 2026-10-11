namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record ResolvedRevision(
		RevisionContext Context,
		int LatestRevisionNumber,
		string BpmnXml,
		string ContentSha256);
}
