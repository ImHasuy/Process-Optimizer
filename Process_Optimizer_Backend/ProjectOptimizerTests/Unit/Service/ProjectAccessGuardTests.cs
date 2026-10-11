using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Service
{
	public sealed class ProjectAccessGuardTests : ServiceTestBase
	{
		[Fact]
		public async Task GetProjectAsync_GuardUnknownProject_ThrowsNotFound()
		{
			var unknownId = Guid.NewGuid();

			var exception = await Assert.ThrowsAsync<NotFoundException>(() => ProjectAccessGuard.GetProjectAsync(unknownId));

			Assert.Equal($"Project with id '{unknownId}' was not found.", exception.Message);
		}

		[Fact]
		public async Task GetProjectAsync_GuardExistingProject_ReturnsTrackedProject()
		{
			var project = await TestData.AddProjectAsync("Launch");
			DbContext.ChangeTracker.Clear();

			var loaded = await ProjectAccessGuard.GetProjectAsync(project.Id);

			Assert.Equal(project.Id, loaded.Id);
			Assert.Equal("Launch", loaded.Name);
			Assert.Equal(EntityState.Unchanged, DbContext.Entry(loaded).State);
		}
	}
}
