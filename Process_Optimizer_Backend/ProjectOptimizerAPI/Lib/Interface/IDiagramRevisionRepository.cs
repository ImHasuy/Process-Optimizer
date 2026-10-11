using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IDiagramRevisionRepository : IRepository<DiagramRevision>
	{
		Task<DiagramRevisionDTO?> FindDtoAsync(Guid projectId, int revisionNumber);
		Task<List<DiagramRevisionListItemDTO>> ListForProjectAsync(Guid projectId);
		Task<DiagramRevision?> FindByNumberAsync(Guid projectId, int revisionNumber);
		Task<List<DiagramRevision>> ListPrunableAsync(Guid projectId, int maxPrunableRevisionNumber, IReadOnlyCollection<int> keptRevisionNumbers);
	}
}
