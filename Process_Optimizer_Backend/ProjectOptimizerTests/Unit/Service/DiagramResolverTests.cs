using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Service
{
	public sealed class DiagramResolverTests : ServiceTestBase
	{
		private const string ProjectCurrency = "HUF";

		[Fact]
		public async Task ResolveAsync_ExistingProject_ReturnsCurrentDiagram()
		{
			var project = await TestData.AddProjectAsync(timeUnit: ETimeUnit.Hours, currency: ProjectCurrency);
			await TestData.SetDiagramAsync(project, BpmnFixtures.WorkedExample);

			var resolved = await DiagramResolver.ResolveAsync(project.Id);

			Assert.Equal(project.Id, resolved.Context.ProjectId);
			Assert.Equal(ETimeUnit.Hours, resolved.Context.TimeUnit);
			Assert.Equal(ProjectCurrency, resolved.Context.Currency);
			Assert.Equal(BpmnFixtures.WorkedExample, resolved.BpmnXml);
			Assert.Equal(ContentHash.Sha256Hex(BpmnFixtures.WorkedExample), resolved.ContentSha256);
		}

		[Fact]
		public async Task ResolveAsync_NewProject_ReturnsDefaultDiagram()
		{
			var project = await TestData.AddProjectAsync();

			var resolved = await DiagramResolver.ResolveAsync(project.Id);

			Assert.Equal(DefaultDiagram.Xml, resolved.BpmnXml);
		}

		[Fact]
		public async Task ResolveAsync_ProjectWithoutDiagram_ThrowsNotFound()
		{
			var project = await TestData.AddProjectAsync();
			await DbContext.Diagrams.Where(diagram => diagram.ProjectId == project.Id).ExecuteDeleteAsync();

			var exception = await Assert.ThrowsAsync<NotFoundException>(() => DiagramResolver.ResolveAsync(project.Id));

			Assert.Equal(ErrorMessages.DiagramNotFound, exception.Message);
		}

		[Fact]
		public async Task ResolveAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => DiagramResolver.ResolveAsync(projectId));
		}
	}
}
