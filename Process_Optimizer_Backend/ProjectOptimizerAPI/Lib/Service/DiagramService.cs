using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class DiagramService(IUnitOfWork unitOfWork, IProjectAccessGuard projectAccessGuard, IDiagramRevisionFactory diagramRevisionFactory, IProcessJsonConverter processJsonConverter, TimeProvider timeProvider) : IDiagramService
	{
		private const string StaleRevisionMessage = "The diagram was changed by another session. Reload it before saving.";
		private const string FileExtension = ".bpmn";
		private const string FallbackFileName = "diagram";
		private const char FileNameReplacement = '_';

		private static readonly HashSet<char> ForbiddenFileNameChars = [.. Path.GetInvalidFileNameChars(), '"', ';'];

		public async Task<DiagramRevisionDTO> GetLatestDiagramAsync(Guid projectId)
		{
			var project = await projectAccessGuard.GetProjectAsync(projectId);
			return await FindRevisionAsync(projectId, project.LatestRevisionNumber);
		}

		public async Task<DiagramRevisionDTO> SaveDiagramAsync(Guid projectId, DiagramRevisionCreateDTO request)
		{
			var project = await projectAccessGuard.GetProjectAsync(projectId);
			return await SaveRevisionAsync(project, request.BpmnXml, request.BaseRevision!.Value);
		}

		public async Task<DiagramRevisionDTO> SaveJsonDiagramAsync(Guid projectId, DiagramJsonRevisionCreateDTO request)
		{
			var project = await projectAccessGuard.GetProjectAsync(projectId);
			var bpmnXml = processJsonConverter.ToBpmnXml(request.ToDiagram(), project.TimeUnit, project.Currency);
			return await SaveRevisionAsync(project, bpmnXml, request.BaseRevision!.Value);
		}

		public async Task<List<DiagramRevisionListItemDTO>> ListRevisionsAsync(Guid projectId)
		{
			await projectAccessGuard.EnsureExistsAsync(projectId);
			return await unitOfWork.DiagramRevisionRepository.ListForProjectAsync(projectId);
		}

		public async Task<DiagramRevisionDTO> GetRevisionAsync(Guid projectId, int revisionNumber)
		{
			await projectAccessGuard.EnsureExistsAsync(projectId);
			return await FindRevisionAsync(projectId, revisionNumber);
		}

		public async Task<DiagramFileDTO> GetDiagramFileAsync(Guid projectId)
		{
			var project = await projectAccessGuard.GetProjectAsync(projectId);
			var revision = EnsureRevisionFound(await unitOfWork.DiagramRevisionRepository.FindByNumberAsync(projectId, project.LatestRevisionNumber));

			return new DiagramFileDTO { FileName = CreateFileName(project.Name), Content = revision.BpmnXml };
		}

		private async Task<DiagramRevisionDTO> SaveRevisionAsync(Project project, string bpmnXml, int baseRevision)
		{
			var now = timeProvider.UtcNow();
			var revision = diagramRevisionFactory.Create(project.Id, project.LatestRevisionNumber + 1, bpmnXml, now);
			EnsureBaseRevisionIsLatest(project, baseRevision);

			unitOfWork.DiagramRevisionRepository.Add(revision);
			project.LatestRevisionNumber = revision.RevisionNumber;
			project.UpdatedAt = now;
			await PruneOldRevisionsAsync(project.Id, revision.RevisionNumber);
			await unitOfWork.SaveChangesOrConflictAsync(StaleRevisionMessage);

			return await FindRevisionAsync(project.Id, revision.RevisionNumber);
		}

		private async Task<DiagramRevisionDTO> FindRevisionAsync(Guid projectId, int revisionNumber) =>
			EnsureRevisionFound(await unitOfWork.DiagramRevisionRepository.FindDtoAsync(projectId, revisionNumber));

		private static void EnsureBaseRevisionIsLatest(Project project, int baseRevision)
		{
			if (baseRevision != project.LatestRevisionNumber)
			{
				throw new ConflictException(StaleRevisionMessage);
			}
		}

		private async Task PruneOldRevisionsAsync(Guid projectId, int newRevisionNumber)
		{
			var maxPrunableRevisionNumber = newRevisionNumber - AnalysisLimits.RevisionsToKeep;
			if (maxPrunableRevisionNumber < 1)
			{
				return;
			}

			var referencedRevisionNumbers = await unitOfWork.SavedScenarioRepository.ListReferencedRevisionNumbersAsync(projectId);
			var prunable = await unitOfWork.DiagramRevisionRepository.ListPrunableAsync(projectId, maxPrunableRevisionNumber, referencedRevisionNumbers);
			prunable.ForEach(unitOfWork.DiagramRevisionRepository.Remove);
		}

		private static TRevision EnsureRevisionFound<TRevision>(TRevision? revision) where TRevision : class =>
			revision ?? throw new NotFoundException(ErrorMessages.RevisionNotFound);

		private static string CreateFileName(string projectName)
		{
			var sanitized = new string(projectName.Trim().Select(character => ForbiddenFileNameChars.Contains(character) ? FileNameReplacement : character).ToArray());
			return (sanitized.Length == 0 ? FallbackFileName : sanitized) + FileExtension;
		}
	}
}
