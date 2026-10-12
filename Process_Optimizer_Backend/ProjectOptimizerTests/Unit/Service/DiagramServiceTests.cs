using System.Text;
using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Service;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerLib.Model.Process;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Service
{
	public sealed class DiagramServiceTests : ServiceTestBase
	{
		private const string TooManyFlowNodesMessage = "The diagram must not contain more than 1000 flow nodes.";
		private const string XmlTooLargeMessage = "The diagram XML must not be larger than 2 MB.";
		private const string NotWellFormedMessage = "The diagram is not well-formed XML or contains a DTD.";
		private const string MalformedXml = "<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"><bpmn:process>";
		private const decimal WorkedExampleMaxCost = 25000m;

		private static readonly TimeSpan SaveDelay = TimeSpan.FromHours(2);

		private readonly DiagramService _service;

		public DiagramServiceTests()
		{
			_service = new DiagramService(UnitOfWork, ProjectAccessGuard, Analysis.DiagramFactory, new ProcessJsonConverter(), TimeProvider);
		}

		[Fact]
		public async Task GetDiagramAsync_NewProject_ReturnsDefaultXml()
		{
			var project = await TestData.AddProjectAsync();

			var diagram = await _service.GetDiagramAsync(project.Id);

			Assert.Equal(DefaultDiagram.Xml, diagram.BpmnXml);
			Assert.Equal(ContentHash.Sha256Hex(DefaultDiagram.Xml), diagram.ContentSha256);
			Assert.Equal(Encoding.UTF8.GetByteCount(DefaultDiagram.Xml), diagram.SizeBytes);
			Assert.Equal(FixedNow.UtcDateTime, diagram.UpdatedAt);
		}

		[Fact]
		public async Task GetDiagramAsync_AfterOverwrite_ReturnsCurrentDiagram()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.SetDiagramAsync(project, BpmnFixtures.WorkedExample);

			var diagram = await _service.GetDiagramAsync(project.Id);

			Assert.Equal(BpmnFixtures.WorkedExample, diagram.BpmnXml);
			Assert.Equal(12, diagram.FlowNodeCount);
		}

		[Fact]
		public async Task GetDiagramAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.GetDiagramAsync(projectId));
		}

		[Fact]
		public async Task GetDiagramAsync_ProjectWithoutDiagram_ThrowsNotFound()
		{
			var project = await TestData.AddProjectAsync();
			await DeleteDiagramAsync(project.Id);

			var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetDiagramAsync(project.Id));

			Assert.Equal(ErrorMessages.DiagramNotFound, exception.Message);
		}

		[Fact]
		public async Task SaveDiagramAsync_ValidDiagram_OverwritesDiagram()
		{
			var project = await TestData.AddProjectAsync();

			var diagram = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample));

			Assert.Equal(BpmnFixtures.WorkedExample, diagram.BpmnXml);
			Assert.Equal(ContentHash.Sha256Hex(BpmnFixtures.WorkedExample), diagram.ContentSha256);
			Assert.Equal(Encoding.UTF8.GetByteCount(BpmnFixtures.WorkedExample), diagram.SizeBytes);
			Assert.Equal(12, diagram.FlowNodeCount);
			Assert.Equal(BpmnFixtures.WorkedExample, (await LoadDiagramAsync(project.Id)).BpmnXml);
			Assert.Equal(1, await CountDiagramsAsync(project.Id));
		}

		[Fact]
		public async Task SaveDiagramAsync_SecondSave_ReplacesXmlAndCounts()
		{
			var project = await TestData.AddProjectAsync();
			await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample));

			var diagram = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.DisconnectedDiagram()));

			var stored = await LoadDiagramAsync(project.Id);
			Assert.Equal(BpmnFixtures.DisconnectedDiagram(), diagram.BpmnXml);
			Assert.Equal(3, diagram.FlowNodeCount);
			Assert.True(diagram.ErrorCount > 0);
			Assert.False(diagram.IsOptimizable);
			Assert.Equal(diagram.BpmnXml, stored.BpmnXml);
			Assert.Equal(diagram.ContentSha256, stored.ContentSha256);
			Assert.Null(stored.MaxCost);
			Assert.Equal(1, await CountDiagramsAsync(project.Id));
		}

		[Fact]
		public async Task SaveDiagramAsync_IdenticalXml_KeepsSingleDiagram()
		{
			var project = await TestData.AddProjectAsync();

			var diagram = await _service.SaveDiagramAsync(project.Id, SaveRequest(DefaultDiagram.Xml));

			Assert.Equal(ContentHash.Sha256Hex(DefaultDiagram.Xml), diagram.ContentSha256);
			Assert.Equal(1, await CountDiagramsAsync(project.Id));
		}

		[Fact]
		public async Task SaveDiagramAsync_DiagramWithSemanticErrors_IsSavedWithIssueCounts()
		{
			var project = await TestData.AddProjectAsync();

			var diagram = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.DisconnectedDiagram()));

			Assert.True(diagram.ErrorCount > 0);
			Assert.False(diagram.IsOptimizable);
		}

		[Fact]
		public async Task SaveDiagramAsync_DiagramWithSemanticErrors_StoresNoLimits()
		{
			var project = await TestData.AddProjectAsync();

			await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.DisconnectedDiagram()));

			var stored = await LoadDiagramAsync(project.Id);
			Assert.Null(stored.MaxCost);
			Assert.Null(stored.MaxDurationBound);
		}

		[Fact]
		public async Task SaveDiagramAsync_ValidDiagram_ReturnsValidationSummary()
		{
			var project = await TestData.AddProjectAsync();

			var diagram = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample));

			Assert.True(diagram.IsOptimizable);
			Assert.Equal(0, diagram.ErrorCount);
		}

		[Fact]
		public async Task SaveDiagramAsync_OptimizableDiagram_StoresMaxCostAndBound()
		{
			var project = await TestData.AddProjectAsync();

			await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample));

			var stored = await LoadDiagramAsync(project.Id);
			Assert.Equal(WorkedExampleMaxCost, stored.MaxCost);
			Assert.NotNull(stored.MaxDurationBound);
			Assert.True(stored.MaxDurationBound > 0);
		}

		[Fact]
		public async Task SaveDiagramAsync_MalformedXml_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(MalformedXml)));

			Assert.Equal(NotWellFormedMessage, exception.Message);
		}

		[Fact]
		public async Task SaveDiagramAsync_DtdPayload_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.Load("Parsing/xxe.bpmn"))));
		}

		[Fact]
		public async Task SaveDiagramAsync_OversizedXml_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(OversizedXml())));

			Assert.Equal(XmlTooLargeMessage, exception.Message);
		}

		[Fact]
		public async Task SaveDiagramAsync_TooManyFlowNodes_ThrowsInvalidRequest()
		{
			var project = await TestData.AddProjectAsync();

			var exception = await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnDiagramBuilder.Chain(AnalysisLimits.MaxFlowNodes))));

			Assert.Equal(TooManyFlowNodesMessage, exception.Message);
		}

		[Fact]
		public async Task SaveDiagramAsync_InvalidXml_LeavesStoredDiagramUnchanged()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.SetDiagramAsync(project, BpmnFixtures.WorkedExample);
			TimeProvider.Advance(SaveDelay);

			await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveDiagramAsync(project.Id, SaveRequest(MalformedXml)));

			var stored = await LoadDiagramAsync(project.Id);
			Assert.Equal(BpmnFixtures.WorkedExample, stored.BpmnXml);
			Assert.Equal(ContentHash.Sha256Hex(BpmnFixtures.WorkedExample), stored.ContentSha256);
			Assert.Equal(12, stored.FlowNodeCount);
			Assert.Equal(FixedNow.UtcDateTime, stored.UpdatedAt);
			Assert.Equal(FixedNow.UtcDateTime, (await LoadProjectAsync(project.Id)).UpdatedAt);
		}

		[Fact]
		public async Task SaveDiagramAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.SaveDiagramAsync(projectId, SaveRequest(MalformedXml)));
		}

		[Fact]
		public async Task SaveDiagramAsync_ValidDiagram_UpdatesTimestamps()
		{
			var project = await TestData.AddProjectAsync();
			TimeProvider.Advance(SaveDelay);

			var diagram = await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample));

			var expectedTime = FixedNow.Add(SaveDelay).UtcDateTime;
			var stored = await LoadProjectAsync(project.Id);
			Assert.Equal(expectedTime, stored.UpdatedAt);
			Assert.Equal(FixedNow.UtcDateTime, stored.CreatedAt);
			Assert.Equal(expectedTime, diagram.UpdatedAt);
			Assert.Equal(expectedTime, (await LoadDiagramAsync(project.Id)).UpdatedAt);
		}

		[Fact]
		public async Task SaveDiagramAsync_OtherProjectDiagram_IsNotChanged()
		{
			var project = await TestData.AddProjectAsync();
			var otherProject = await TestData.AddProjectAsync();

			await _service.SaveDiagramAsync(project.Id, SaveRequest(BpmnFixtures.WorkedExample));

			Assert.Equal(DefaultDiagram.Xml, (await LoadDiagramAsync(otherProject.Id)).BpmnXml);
		}

		[Fact]
		public async Task GetDiagramFileAsync_ExistingProject_ReturnsCurrentXmlAndBpmnFileName()
		{
			var project = await TestData.AddProjectAsync(name: "Roadmap 2026");
			await TestData.SetDiagramAsync(project, BpmnFixtures.WorkedExample);

			var file = await _service.GetDiagramFileAsync(project.Id);

			Assert.Equal("Roadmap 2026.bpmn", file.FileName);
			Assert.Equal(BpmnFixtures.WorkedExample, file.Content);
		}

		[Fact]
		public async Task GetDiagramFileAsync_NameWithForbiddenCharacters_ReplacesThem()
		{
			var project = await TestData.AddProjectAsync(name: "  Plan/\"A\";B  ");

			var file = await _service.GetDiagramFileAsync(project.Id);

			Assert.Equal("Plan__A__B.bpmn", file.FileName);
		}

		[Fact]
		public async Task GetDiagramFileAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.GetDiagramFileAsync(projectId));
		}

		[Fact]
		public async Task SaveJsonDiagramAsync_SampleDiagram_OverwritesWithConvertedXml()
		{
			var project = await TestData.AddProjectAsync();

			var diagram = await _service.SaveJsonDiagramAsync(project.Id, JsonSaveRequest());

			Assert.Equal(3, diagram.FlowNodeCount);
			Assert.True(diagram.IsOptimizable);
			Assert.Equal(0, diagram.WarningCount);
			Assert.Contains("po:duration=\"0.010417\"", diagram.BpmnXml);
			Assert.Equal(diagram.BpmnXml, (await LoadDiagramAsync(project.Id)).BpmnXml);
			Assert.Equal(1, await CountDiagramsAsync(project.Id));
		}

		[Fact]
		public async Task SaveJsonDiagramAsync_ProjectInHoursAndUsd_ConvertsWithProjectSettings()
		{
			var project = await TestData.AddProjectAsync(timeUnit: ETimeUnit.Hours, currency: "USD");
			var request = JsonSaveRequest();
			request.States[1].Price = "$12.50";

			var diagram = await _service.SaveJsonDiagramAsync(project.Id, request);

			Assert.Contains("po:duration=\"0.25\"", diagram.BpmnXml);
			Assert.Contains("po:cost=\"12.5\"", diagram.BpmnXml);
			Assert.Equal(0, diagram.ErrorCount);
		}

		[Fact]
		public async Task SaveJsonDiagramAsync_InvalidId_ThrowsInvalidRequestAndLeavesDiagramUnchanged()
		{
			var project = await TestData.AddProjectAsync();
			await TestData.SetDiagramAsync(project, BpmnFixtures.WorkedExample);
			var request = JsonSaveRequest();
			request.States[0].Id = "1 bad";

			await Assert.ThrowsAsync<InvalidRequestException>(() => _service.SaveJsonDiagramAsync(project.Id, request));

			Assert.Equal(BpmnFixtures.WorkedExample, (await LoadDiagramAsync(project.Id)).BpmnXml);
		}

		[Fact]
		public async Task SaveJsonDiagramAsync_UnknownProject_ThrowsNotFound()
		{
			var projectId = Guid.NewGuid();

			await Assert.ThrowsAsync<NotFoundException>(() => _service.SaveJsonDiagramAsync(projectId, JsonSaveRequest()));
		}

		private static ProcessDiagram JsonSaveRequest() =>
			ProcessJsonFixtures.Diagram(ProcessJsonFixtures.SampleStates(), ProcessJsonFixtures.SampleBranches());

		private static DiagramSaveDTO SaveRequest(string bpmnXml) =>
			new() { BpmnXml = bpmnXml };

		private static string OversizedXml() =>
			DefaultDiagram.Xml + $"<!--{new string('x', AnalysisLimits.MaxXmlBytes)}-->";

		private Task<int> CountDiagramsAsync(Guid projectId) =>
			DbContext.Diagrams.CountAsync(diagram => diagram.ProjectId == projectId);

		private Task<int> DeleteDiagramAsync(Guid projectId) =>
			DbContext.Diagrams.Where(diagram => diagram.ProjectId == projectId).ExecuteDeleteAsync();
	}
}
