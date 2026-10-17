using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IProjectAccessGuard
	{
		Task<Project> GetProjectAsync(Guid projectId);
		Task EnsureExistsAsync(Guid projectId);
	}
}
