using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;
using ProjectOptimizerLib.Model.Process;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class DiagramService(IUnitOfWork unitOfWork, IProjectAccessGuard projectAccessGuard, IDiagramFactory diagramFactory, IProcessJsonConverter processJsonConverter, TimeProvider timeProvider) : IDiagramService
	{
		private const string FileExtension = ".bpmn";
		private const string FallbackFileName = "diagram";
		private const char FileNameReplacement = '_';

		private static readonly HashSet<char> ForbiddenFileNameChars = [.. Path.GetInvalidFileNameChars(), '"', ';'];

		public async Task<DiagramDTO> GetDiagramAsync(Guid projectId)
		{
			await projectAccessGuard.EnsureExistsAsync(projectId);
			return EnsureFound(await unitOfWork.DiagramRepository.FindDtoAsync(projectId));
		}

		public async Task<DiagramDTO> SaveDiagramAsync(Guid projectId, DiagramSaveDTO request)
		{
			var project = await projectAccessGuard.GetProjectAsync(projectId);
			return await OverwriteAsync(project, request.BpmnXml);
		}

		public async Task<DiagramDTO> SaveJsonDiagramAsync(Guid projectId, ProcessDiagram request)
		{
			var project = await projectAccessGuard.GetProjectAsync(projectId);
			var bpmnXml = processJsonConverter.ToBpmnXml(request, project.TimeUnit, project.Currency);
			return await OverwriteAsync(project, bpmnXml);
		}

		public async Task<DiagramFileDTO> GetDiagramFileAsync(Guid projectId)
		{
			var project = await projectAccessGuard.GetProjectAsync(projectId);
			var diagram = EnsureFound(await unitOfWork.DiagramRepository.FindAsync(projectId));

			return new DiagramFileDTO { FileName = CreateFileName(project.Name), Content = diagram.BpmnXml };
		}

		private async Task<DiagramDTO> OverwriteAsync(Project project, string bpmnXml)
		{
			var diagram = EnsureFound(await unitOfWork.DiagramRepository.FindAsync(project.Id));
			var now = timeProvider.UtcNow();
			diagramFactory.Apply(diagram, bpmnXml, now);
			project.UpdatedAt = now;
			await unitOfWork.SaveChangesAsync();

			return EnsureFound(await unitOfWork.DiagramRepository.FindDtoAsync(project.Id));
		}

		private static TDiagram EnsureFound<TDiagram>(TDiagram? diagram) where TDiagram : class =>
			diagram ?? throw new NotFoundException(ErrorMessages.DiagramNotFound);

		private static string CreateFileName(string projectName)
		{
			var sanitized = new string(projectName.Trim().Select(character => ForbiddenFileNameChars.Contains(character) ? FileNameReplacement : character).ToArray());
			return (sanitized.Length == 0 ? FallbackFileName : sanitized) + FileExtension;
		}
	}
}
