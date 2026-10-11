using System.Text;
using Microsoft.AspNetCore.Mvc;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Controllers
{
	[Route("api/projects/{projectId:guid}/diagram")]
	public sealed class DiagramController(IDiagramService diagramService) : ApiControllerBase
	{
		private const string XmlContentType = "application/xml";

		/// <summary>Returns the latest revision of the project's BPMN diagram, including the XML and its validation summary.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <returns>The latest diagram revision.</returns>
		/// <response code="200">The latest diagram revision.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		[HttpGet]
		[ProducesResponseType(typeof(ApiResponse<DiagramRevisionDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetLatestDiagram(Guid projectId) => Success(await diagramService.GetLatestDiagramAsync(projectId));

		/// <summary>Saves the diagram as a new revision on top of the given base revision. Semantic diagram issues never block the save; they are reported in the revision's error and warning counts.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="request">The BPMN XML to store and the revision number it was edited from (must be the latest revision).</param>
		/// <returns>The newly created revision.</returns>
		/// <response code="201">The revision was created; the Location header points to it.</response>
		/// <response code="400">The body is invalid, the XML is not well-formed or not a BPMN document, contains a DTD, exceeds 2 MiB or has more than 1000 flow nodes.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="409">The base revision is not the latest revision because the diagram was changed by another session.</response>
		/// <response code="413">The request body exceeds the maximum request size.</response>
		[HttpPost("revisions")]
		[RequestSizeLimit(AnalysisLimits.MaxSaveRequestBytes)]
		[ProducesResponseType(typeof(ApiResponse<DiagramRevisionDTO>), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status413PayloadTooLarge)]
		public async Task<IActionResult> SaveDiagram(Guid projectId, [FromBody] DiagramRevisionCreateDTO request)
		{
			var revision = await diagramService.SaveDiagramAsync(projectId, request);
			return Created(nameof(GetRevision), new { projectId, revisionNumber = revision.RevisionNumber }, revision);
		}

		/// <summary>Saves a diagram sent in the frontend's JSON export format (states and branches) as a new revision. The JSON is converted to BPMN XML with a generated layout; time and price texts such as "15m" or "25 EUR" are converted to the project's time unit and currency, and values that cannot be converted are kept as typed and reported as DV-09 issues.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="request">The states and branches of the frontend export and the revision number they were edited from (must be the latest revision).</param>
		/// <returns>The newly created revision with the generated BPMN XML.</returns>
		/// <response code="201">The revision was created; the Location header points to it.</response>
		/// <response code="400">The body is invalid, an id is not a valid BPMN identifier or is used twice, a state type is not a BPMN element type, a state has several default branches, or the generated XML exceeds the diagram limits.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="409">The base revision is not the latest revision because the diagram was changed by another session.</response>
		/// <response code="413">The request body exceeds the maximum request size.</response>
		[HttpPost("revisions/json")]
		[RequestSizeLimit(AnalysisLimits.MaxSaveRequestBytes)]
		[ProducesResponseType(typeof(ApiResponse<DiagramRevisionDTO>), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status413PayloadTooLarge)]
		public async Task<IActionResult> SaveJsonDiagram(Guid projectId, [FromBody] DiagramJsonRevisionCreateDTO request)
		{
			var revision = await diagramService.SaveJsonDiagramAsync(projectId, request);
			return Created(nameof(GetRevision), new { projectId, revisionNumber = revision.RevisionNumber }, revision);
		}

		/// <summary>Lists the stored revisions of the project's diagram, newest first, without the XML.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <returns>The revisions ordered by descending revision number.</returns>
		/// <response code="200">The list of revisions.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		[HttpGet("revisions")]
		[ProducesResponseType(typeof(ApiResponse<List<DiagramRevisionListItemDTO>>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> ListRevisions(Guid projectId) => Success(await diagramService.ListRevisionsAsync(projectId));

		/// <summary>Returns one stored revision of the project's diagram, including the XML.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="revisionNumber">The number of the revision.</param>
		/// <returns>The requested revision.</returns>
		/// <response code="200">The requested revision.</response>
		/// <response code="404">No project exists with the given identifier or the revision does not exist.</response>
		[HttpGet("revisions/{revisionNumber:int}")]
		[ProducesResponseType(typeof(ApiResponse<DiagramRevisionDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetRevision(Guid projectId, int revisionNumber) =>
			Success(await diagramService.GetRevisionAsync(projectId, revisionNumber));

		/// <summary>Downloads the latest diagram revision as a .bpmn file named after the project. The body is the raw XML without the response envelope.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <returns>The BPMN XML of the latest revision as a file attachment.</returns>
		/// <response code="200">The BPMN XML file.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		[HttpGet("file")]
		[ProducesResponseType(typeof(string), StatusCodes.Status200OK, XmlContentType)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound, "application/json")]
		public async Task<IActionResult> DownloadDiagram(Guid projectId)
		{
			var file = await diagramService.GetDiagramFileAsync(projectId);
			return File(Encoding.UTF8.GetBytes(file.Content), XmlContentType, file.FileName);
		}
	}
}
