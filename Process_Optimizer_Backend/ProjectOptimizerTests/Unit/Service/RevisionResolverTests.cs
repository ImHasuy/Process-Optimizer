using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Service
{
	public sealed class RevisionResolverTests : ServiceTestBase
	{
		private const string ProjectCurrency = "HUF";

		[Fact]
		public async Task ResolveAsync_NoRevision_ReturnsLatestRevision()
		{
			var project = await TestData.AddProjectAsync(timeUnit: ETimeUnit.Hours, currency: ProjectCurrency);
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var resolved = await RevisionResolver.ResolveAsync(project.Id, null);

			Assert.Equal(project.Id, resolved.Context.ProjectId);
			Assert.Equal(2, resolved.Context.RevisionNumber);
			Assert.Equal(ETimeUnit.Hours, resolved.Context.TimeUnit);
			Assert.Equal(ProjectCurrency, resolved.Context.Currency);
			Assert.Equal(2, resolved.LatestRevisionNumber);
			Assert.Equal(BpmnFixtures.WorkedExample, resolved.BpmnXml);
			Assert.Equal(ContentHash.Sha256Hex(BpmnFixtures.WorkedExample), resolved.ContentSha256);
		}

		[Fact]
		public async Task ResolveAsync_SpecificRevision_ReturnsThatRevision()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(project, BpmnFixtures.WorkedExample);

			var resolved = await RevisionResolver.ResolveAsync(project.Id, 1);

			Assert.Equal(1, resolved.Context.RevisionNumber);
			Assert.Equal(2, resolved.LatestRevisionNumber);
			Assert.Equal(DefaultDiagram.Xml, resolved.BpmnXml);
		}

		[Fact]
		public async Task ResolveAsync_UnknownRevision_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<InvalidRequestException>(() => RevisionResolver.ResolveAsync(project.Id, 2));

			Assert.Equal(ErrorMessages.RevisionNotFound, exception.Message);
		}

		[Fact]
		public async Task ResolveAsync_RevisionOfOtherProject_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();
			var otherProject = await TestData.AddProjectAsync();
			await TestData.AddRevisionAsync(otherProject, BpmnFixtures.WorkedExample);

			await Assert.ThrowsAsync<InvalidRequestException>(() => RevisionResolver.ResolveAsync(project.Id, 2));
		}

		[Fact]
		public async Task ResolveAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => RevisionResolver.ResolveAsync(projectId, 1));
		}
	}
}
