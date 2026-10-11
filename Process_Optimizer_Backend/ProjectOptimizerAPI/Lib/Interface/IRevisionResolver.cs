using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IRevisionResolver
	{
		Task<ResolvedRevision> ResolveAsync(Guid projectId, int? revisionNumber);
	}
}
