using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class DiagramResolver(IUnitOfWork unitOfWork, IProjectAccessGuard projectAccessGuard) : IDiagramResolver
	{
		public async Task<ResolvedDiagram> ResolveAsync(Guid projectId)
		{
			var project = await projectAccessGuard.GetProjectAsync(projectId);
			var diagram = await unitOfWork.DiagramRepository.FindAsync(projectId)
				?? throw new NotFoundException(ErrorMessages.DiagramNotFound);

			return new ResolvedDiagram(
				new ProjectContext(project.Id, project.TimeUnit, project.Currency),
				diagram.BpmnXml,
				diagram.ContentSha256);
		}
	}
}
