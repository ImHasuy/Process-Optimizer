using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record RevisionContext(
		Guid ProjectId,
		int RevisionNumber,
		ETimeUnit TimeUnit,
		string Currency);
}
