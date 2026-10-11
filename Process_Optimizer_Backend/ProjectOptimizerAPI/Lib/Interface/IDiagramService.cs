using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IDiagramService
	{
		Task<DiagramRevisionDTO> GetLatestDiagramAsync(Guid projectId);
		Task<DiagramRevisionDTO> SaveDiagramAsync(Guid projectId, DiagramRevisionCreateDTO request);
		Task<DiagramRevisionDTO> SaveJsonDiagramAsync(Guid projectId, DiagramJsonRevisionCreateDTO request);
		Task<List<DiagramRevisionListItemDTO>> ListRevisionsAsync(Guid projectId);
		Task<DiagramRevisionDTO> GetRevisionAsync(Guid projectId, int revisionNumber);
		Task<DiagramFileDTO> GetDiagramFileAsync(Guid projectId);
	}
}
