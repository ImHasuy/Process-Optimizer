using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IDiagramResolver
	{
		Task<ResolvedDiagram> ResolveAsync(Guid projectId);
	}
}
