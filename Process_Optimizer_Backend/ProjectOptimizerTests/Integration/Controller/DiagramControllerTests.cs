using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ProjectOptimizerAPI.Controllers;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Integration.Controller
{
	public sealed class DiagramControllerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
	{
		private const string StaleRevisionMessage = "The diagram was changed by another session. Reload it before saving.";
		private const string XmlContentType = "application/xml";
		private const string AttachmentDisposition = "attachment";

		private readonly HttpClient _client = factory.CreateClient();

		[Fact]
		public async Task GetLatestDiagram_NewProject_ReturnsRevisionOneWithDefaultXml()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(DiagramUrl(project.Id));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.ReadApiResponseAsync<DiagramRevisionDTO>();
			Assert.Equal((int)HttpStatusCode.OK, body.StatusCode);
			Assert.Equal(1, body.Data!.RevisionNumber);
			Assert.Equal(DefaultDiagram.Xml, body.Data.BpmnXml);
		}

		[Fact]
		public async Task GetLatestDiagram_AfterSave_ReturnsSavedRevision()
		{
			var project = await _client.CreateProjectAsync();
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample, 1);

			var response = await _client.GetAsync(DiagramUrl(project.Id));

			var latest = (await response.ReadApiResponseAsync<DiagramRevisionDTO>()).Data!;
			Assert.Equal(2, latest.RevisionNumber);
			Assert.Equal(BpmnFixtures.WorkedExample, latest.BpmnXml);
		}

		[Fact]
		public async Task GetLatestDiagram_UnknownProject_ReturnsNotFoundEnvelope()
		{
			var response = await _client.GetAsync(DiagramUrl(Guid.NewGuid()));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task SaveDiagram_ValidBody_ReturnsCreatedWithLocationToRevision()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(RevisionsUrl(project.Id), SaveRequest(BpmnFixtures.WorkedExample, 1));

			Assert.Equal(HttpStatusCode.Created, response.StatusCode);
			var body = await response.ReadApiResponseAsync<DiagramRevisionDTO>();
			Assert.Equal((int)HttpStatusCode.Created, body.StatusCode);
			Assert.Equal(2, body.Data!.RevisionNumber);
			Assert.NotNull(response.Headers.Location);
			Assert.EndsWith(RevisionUrl(project.Id, 2), response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task SaveDiagram_LocationHeader_ReturnsSavedRevision()
		{
			var project = await _client.CreateProjectAsync();
			var saveResponse = await _client.PostAsJsonAsync(RevisionsUrl(project.Id), SaveRequest(BpmnFixtures.WorkedExample, 1));

			var response = await _client.GetAsync(saveResponse.Headers.Location);

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var revision = (await response.ReadApiResponseAsync<DiagramRevisionDTO>()).Data!;
			Assert.Equal(2, revision.RevisionNumber);
			Assert.Equal(BpmnFixtures.WorkedExample, revision.BpmnXml);
		}

		[Fact]
		public async Task SaveDiagram_ResponseContainsValidationSummary()
		{
			var project = await _client.CreateProjectAsync();

			var revision = await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample, 1);

			Assert.True(revision.IsOptimizable);
			Assert.Equal(0, revision.ErrorCount);
			Assert.Equal(12, revision.FlowNodeCount);
			Assert.Equal(Encoding.UTF8.GetByteCount(BpmnFixtures.WorkedExample), revision.SizeBytes);
			Assert.False(string.IsNullOrEmpty(revision.ContentSha256));
		}

		[Fact]
		public async Task SaveDiagram_SemanticErrors_ReturnsCreatedWithErrorCount()
		{
			var project = await _client.CreateProjectAsync();

			var revision = await _client.SaveDiagramAsync(project.Id, BpmnFixtures.DisconnectedDiagram(), 1);

			Assert.True(revision.ErrorCount > 0);
			Assert.False(revision.IsOptimizable);
		}

		[Fact]
		public async Task SaveDiagram_MissingBaseRevision_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(RevisionsUrl(project.Id), new { BpmnXml = DefaultDiagram.Xml });

			await response.AssertValidationErrorAsync(nameof(DiagramRevisionCreateDTO.BaseRevision));
		}

		[Fact]
		public async Task SaveDiagram_ZeroBaseRevision_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(RevisionsUrl(project.Id), SaveRequest(DefaultDiagram.Xml, 0));

			await response.AssertValidationErrorAsync(nameof(DiagramRevisionCreateDTO.BaseRevision));
		}

		[Fact]
		public async Task SaveDiagram_EmptyXml_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(RevisionsUrl(project.Id), SaveRequest(string.Empty, 1));

			await response.AssertValidationErrorAsync(nameof(DiagramRevisionCreateDTO.BpmnXml));
		}

		[Fact]
		public async Task SaveDiagram_MalformedXml_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(RevisionsUrl(project.Id), SaveRequest("<bpmn:definitions", 1));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest);
		}

		[Fact]
		public async Task SaveDiagram_XxePayload_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(RevisionsUrl(project.Id), SaveRequest(BpmnFixtures.Load("Parsing/xxe.bpmn"), 1));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest);
		}

		[Fact]
		public async Task SaveDiagram_BillionLaughsPayload_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(RevisionsUrl(project.Id), SaveRequest(BpmnFixtures.Load("Parsing/billion-laughs.bpmn"), 1));

			await response.AssertApiErrorAsync(HttpStatusCode.BadRequest);
		}

		[Fact]
		public async Task SaveDiagram_OversizedXml_ReturnsBadRequest()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.PostAsJsonAsync(RevisionsUrl(project.Id), SaveRequest(PaddedXml(AnalysisLimits.MaxXmlBytes), 1));

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
		public async Task SaveDiagram_StaleBaseRevision_ReturnsConflict()
		{
			var project = await _client.CreateProjectAsync();
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample, 1);

			var response = await _client.PostAsJsonAsync(RevisionsUrl(project.Id), SaveRequest(DefaultDiagram.Xml, 1));

			var body = await response.AssertApiErrorAsync(HttpStatusCode.Conflict);
			Assert.Equal(StaleRevisionMessage, body.Message);
		}

		[Fact]
		public async Task SaveDiagram_UnknownProject_ReturnsNotFound()
		{
			var response = await _client.PostAsJsonAsync(RevisionsUrl(Guid.NewGuid()), SaveRequest(DefaultDiagram.Xml, 1));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task ListRevisions_AfterSave_ReturnsNewestFirst()
		{
			var project = await _client.CreateProjectAsync();
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample, 1);

			var response = await _client.GetAsync(RevisionsUrl(project.Id));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.ReadApiResponseAsync<List<DiagramRevisionListItemDTO>>();
			Assert.Equal((int)HttpStatusCode.OK, body.StatusCode);
			Assert.Equal(new[] { 2, 1 }, body.Data!.Select(revision => revision.RevisionNumber));
		}

		[Fact]
		public async Task ListRevisions_UnknownProject_ReturnsNotFound()
		{
			var response = await _client.GetAsync(RevisionsUrl(Guid.NewGuid()));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task GetRevision_ExistingNumber_ReturnsRevision()
		{
			var project = await _client.CreateProjectAsync();
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample, 1);

			var response = await _client.GetAsync(RevisionUrl(project.Id, 1));

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var revision = (await response.ReadApiResponseAsync<DiagramRevisionDTO>()).Data!;
			Assert.Equal(1, revision.RevisionNumber);
			Assert.Equal(DefaultDiagram.Xml, revision.BpmnXml);
		}

		[Fact]
		public async Task GetRevision_UnknownNumber_ReturnsNotFound()
		{
			var project = await _client.CreateProjectAsync();

			var response = await _client.GetAsync(RevisionUrl(project.Id, 99));

			var body = await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
			Assert.Equal(ErrorMessages.RevisionNotFound, body.Message);
		}

		[Fact]
		public async Task GetRevision_UnknownProject_ReturnsNotFound()
		{
			var response = await _client.GetAsync(RevisionUrl(Guid.NewGuid(), 1));

			await response.AssertApiErrorAsync(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task DownloadDiagram_ExistingProject_ReturnsXmlAttachment()
		{
			var projectName = $"Download-{Guid.NewGuid():N}";
			var project = await _client.CreateProjectAsync(projectName);
			await _client.SaveDiagramAsync(project.Id, BpmnFixtures.WorkedExample, 1);

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

		private static string RevisionsUrl(Guid projectId) => $"{DiagramUrl(projectId)}/revisions";

		private static string RevisionUrl(Guid projectId, int revisionNumber) => $"{RevisionsUrl(projectId)}/{revisionNumber}";

		private static string FileUrl(Guid projectId) => $"{DiagramUrl(projectId)}/file";

		private static object SaveRequest(string bpmnXml, int baseRevision) => new { BpmnXml = bpmnXml, BaseRevision = baseRevision };

		private static string PaddedXml(int minimumBytes) =>
			DefaultDiagram.Xml + $"<!--{new string('x', minimumBytes)}-->";
	}
}
