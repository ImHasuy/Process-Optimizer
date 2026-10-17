using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ProjectOptimizerAPI.Controllers;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Integration.Controller
{
	public sealed class DiagramControllerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
	{
		private const string XmlContentType = "application/xml";
		private const string AttachmentDisposition = "attachment";

		private readonly HttpClient _client = factory.CreateClient();

		[Fact]
		public async Task GetDiagram_NewProject_ReturnsDefaultXml()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(DiagramUrl(project.Id));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.ReadApiResponseAsync<DiagramDTO>();
			Assert.Equal((int)HttpStatusCode.OK, body.StatusCode);
			Assert.Equal(DefaultDiagram.Xml, body.Data!.BpmnXml);
			Assert.Equal(project.CreatedAt, body.Data.UpdatedAt);
		}

		[Fact]
		public async Task GetDiagram_AfterSave_ReturnsSavedDiagram()
		{
			var project = await _client.CreateProjectAsync();
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(DiagramUrl(project.Id));

			var diagram = (await response.ReadApiResponseAsync<DiagramDTO>()).Data!;
			Assert.Equal(BpmnFixtures.WorkedExample, diagram.BpmnXml);
			Assert.Equal(12, diagram.FlowNodeCount);
		}

		[Fact]
		public async Task GetDiagram_UnknownProject_ReturnsNotFoundEnvelope()
		{
			var response = await _client.GetAsync(DiagramUrl(Guid.NewGuid()));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task SaveDiagram_ValidBody_ReturnsOkWithDiagram()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PutAsJsonAsync(DiagramUrl(project.Id), SaveRequest(BpmnFixtures.WorkedExample));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.ReadApiResponseAsync<DiagramDTO>();
			Assert.Equal((int)HttpStatusCode.OK, body.StatusCode);
			Assert.Equal(BpmnFixtures.WorkedExample, body.Data!.BpmnXml);
		}

		[Fact]
		public async Task SaveDiagram_SecondSave_OverwritesDiagram()
		{
			var project = await _client.CreateProjectAsync();
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample);

			var saved = await _client.SaveDiagramAsync(project.Id, BpmnFixtures.DisconnectedDiagram());

			var current = (await (await _client.GetAsync(DiagramUrl(project.Id))).ReadApiResponseAsync<DiagramDTO>()).Data!;
			Assert.Equal(BpmnFixtures.DisconnectedDiagram(), current.BpmnXml);
			Assert.Equal(saved.ContentSha256, current.ContentSha256);
			Assert.Equal(3, current.FlowNodeCount);
			Assert.False(current.IsOptimizable);
		}

		[Fact]
		public async Task SaveDiagram_ResponseContainsValidationSummary()
		{
			var project = await _client.CreateProjectAsync();

			var diagram = await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample);

			Assert.True(diagram.IsOptimizable);
			Assert.Equal(0, diagram.ErrorCount);
			Assert.Equal(12, diagram.FlowNodeCount);
			Assert.Equal(Encoding.UTF8.GetByteCount(BpmnFixtures.WorkedExample), diagram.SizeBytes);
			Assert.False(string.IsNullOrEmpty(diagram.ContentSha256));
		}

		[Fact]
		public async Task SaveDiagram_SemanticErrors_ReturnsOkWithErrorCount()
		{
			var project = await _client.CreateProjectAsync();

			var diagram = await _client.SaveDiagramAsync(project.Id, BpmnFixtures.DisconnectedDiagram());

			Assert.True(diagram.ErrorCount > 0);
			Assert.False(diagram.IsOptimizable);
		}

		[Fact]
		public async Task SaveDiagram_EmptyXml_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PutAsJsonAsync(DiagramUrl(project.Id), SaveRequest(string.Empty));

			await response.AssertValidationErrorAsync(nameof(DiagramSaveDTO.BpmnXml));
		}

		[Fact]
		public async Task SaveDiagram_MalformedXml_ReturnsBadRequestAndKeepsDiagram()
		{
			var project = await _client.CreateProjectAsync();
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample);

			var response = await _client.PutAsJsonAsync(DiagramUrl(project.Id), SaveRequest("<bpmn:definitions"));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest);
			var current = (await (await _client.GetAsync(DiagramUrl(project.Id))).ReadApiResponseAsync<DiagramDTO>()).Data!;
			Assert.Equal(BpmnFixtures.WorkedExample, current.BpmnXml);
		}

		[Fact]
		public async Task SaveDiagram_XxePayload_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PutAsJsonAsync(DiagramUrl(project.Id), SaveRequest(BpmnFixtures.Load("Parsing/xxe.bpmn")));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest);
		}

		[Fact]
		public async Task SaveDiagram_BillionLaughsPayload_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PutAsJsonAsync(DiagramUrl(project.Id), SaveRequest(BpmnFixtures.Load("Parsing/billion-laughs.bpmn")));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest);
		}

		[Fact]
		public async Task SaveDiagram_OversizedXml_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PutAsJsonAsync(DiagramUrl(project.Id), SaveRequest(PaddedXml(AnalysisLimits.MaxXmlBytes)));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest);
		}

		[Fact]
		public void SaveDiagram_Endpoint_HasThreeMegabyteRequestSizeLimit()
		{
			var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

			var saveEndpoint = endpoints.Single(endpoint => endpoint.DisplayName!.Contains($"{nameof(DiagramController)}.{nameof(DiagramController.SaveDiagram)}"));

			Assert.Equal(AnalysisLimits.MaxSaveRequestBytes, saveEndpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()!.MaxRequestBodySize);
		}

		[Fact]
		public async Task SaveDiagram_UnknownProject_ReturnsNotFound()
		{
			var response = await _client.PutAsJsonAsync(DiagramUrl(Guid.NewGuid()), SaveRequest(DefaultDiagram.Xml));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task DownloadDiagram_ExistingProject_ReturnsXmlAttachment()
		{
			var projectName = $"Download-{Guid.NewGuid():N}";
			var project = await _client.CreateProjectAsync(projectName);
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample);

			var response = await _client.GetAsync(FileUrl(project.Id));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Equal(XmlContentType, response.Content.Headers.ContentType!.MediaType);
			var disposition = response.Content.Headers.ContentDisposition!;
			Assert.Equal(AttachmentDisposition, disposition.DispositionType);
			Assert.Equal($"{projectName}.bpmn", disposition.FileName!.Trim('"'));
			Assert.Equal(BpmnFixtures.WorkedExample, await response.Content.ReadAsStringAsync());
		}

		[Fact]
		public async Task DownloadDiagram_UnknownProject_ReturnsNotFoundEnvelope()
		{
			var response = await _client.GetAsync(FileUrl(Guid.NewGuid()));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		private static string DiagramUrl(Guid projectId) => $"/api/projects/{projectId}/diagram";

		private static string FileUrl(Guid projectId) => $"{DiagramUrl(projectId)}/file";

		private static object SaveRequest(string bpmnXml) => new { BpmnXml = bpmnXml };

		private static string PaddedXml(int minimumBytes) =>
			DefaultDiagram.Xml + $"<!--{new string('x', minimumBytes)}-->";
	}
}
