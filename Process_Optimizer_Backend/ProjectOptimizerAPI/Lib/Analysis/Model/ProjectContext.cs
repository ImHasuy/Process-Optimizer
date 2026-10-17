using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record ProjectContext(
		Guid ProjectId,
		ETimeUnit TimeUnit,
		string Currency);
}
