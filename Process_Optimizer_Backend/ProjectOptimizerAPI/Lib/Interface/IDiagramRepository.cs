using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IDiagramRepository
	{
		Task<Diagram?> FindAsync(Guid projectId);
		Task<DiagramDTO?> FindDtoAsync(Guid projectId);
	}
}
