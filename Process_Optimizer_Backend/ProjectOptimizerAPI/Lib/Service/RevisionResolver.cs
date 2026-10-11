using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class RevisionResolver(IUnitOfWork unitOfWork, IProjectAccessGuard projectAccessGuard) : IRevisionResolver
	{
		public async Task<ResolvedRevision> ResolveAsync(Guid projectId, int? revisionNumber)
		{
			var project = await projectAccessGuard.GetProjectAsync(projectId);
			var number = revisionNumber ?? project.LatestRevisionNumber;
			var revision = EnsureRevisionExists(await unitOfWork.DiagramRevisionRepository.FindByNumberAsync(project.Id, number));

			return new ResolvedRevision(
				new RevisionContext(project.Id, number, project.TimeUnit, project.Currency),
				project.LatestRevisionNumber,
				revision.BpmnXml,
				revision.ContentSha256);
		}

		private static DiagramRevision EnsureRevisionExists(DiagramRevision? revision) =>
			revision ?? throw new InvalidRequestException(ErrorMessages.RevisionNotFound);
	}
}
