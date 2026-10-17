using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class ProjectAccessGuard(IUnitOfWork unitOfWork) : IProjectAccessGuard
	{
		public Task<Project> GetProjectAsync(Guid projectId) => unitOfWork.ProjectRepository.GetRequiredAsync(projectId);

		public async Task EnsureExistsAsync(Guid projectId)
		{
			if (!await unitOfWork.ProjectRepository.ExistsAsync(projectId))
			{
				throw NotFoundException.For<Project>(projectId);
			}
		}
	}
}
