using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model.Process;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IDiagramService
	{
		Task<DiagramDTO> GetDiagramAsync(Guid projectId);
		Task<DiagramDTO> SaveDiagramAsync(Guid projectId, DiagramSaveDTO request);
		Task<DiagramDTO> SaveJsonDiagramAsync(Guid projectId, ProcessDiagram request);
		Task<DiagramFileDTO> GetDiagramFileAsync(Guid projectId);
	}
}
