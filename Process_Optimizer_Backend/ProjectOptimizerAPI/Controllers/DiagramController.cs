using System.Text;
using Microsoft.AspNetCore.Mvc;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Model.Process;

namespace ProjectOptimizerAPI.Controllers
{
	[Route("api/projects/{projectId:guid}/diagram")]
	public sealed class DiagramController(IDiagramService diagramService) : ApiControllerBase
	{
		private const string XmlContentType = "application/xml";

		/// <summary>Returns the project's diagram, including the BPMN XML and its validation summary.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <returns>The current diagram.</returns>
		/// <response code="200">The current diagram.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		[HttpGet]
		[ProducesResponseType(typeof(ApiResponse<DiagramDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetDiagram(Guid projectId) => Success(await diagramService.GetDiagramAsync(projectId));

		/// <summary>Overwrites the project's diagram with a BPMN XML. Semantic diagram issues never block the save; they are reported in the error and warning counts.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="request">The BPMN XML to store.</param>
		/// <returns>The saved diagram.</returns>
		/// <response code="200">The diagram was overwritten.</response>
		/// <response code="400">The body is invalid, the XML is not well-formed or not a BPMN document, contains a DTD, exceeds 2 MiB or has more than 1000 flow nodes.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="413">The request body exceeds the maximum request size.</response>
		[HttpPut]
		[RequestSizeLimit(AnalysisLimits.MaxSaveRequestBytes)]
		[ProducesResponseType(typeof(ApiResponse<DiagramDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status413PayloadTooLarge)]
		public async Task<IActionResult> SaveDiagram(Guid projectId, [FromBody] DiagramSaveDTO request) =>
			Success(await diagramService.SaveDiagramAsync(projectId, request));

		/// <summary>Overwrites the project's diagram with the frontend's JSON export (states and branches). The JSON is converted to BPMN XML with a generated layout; time and price texts such as "15m" or "25 EUR" are converted to the project's time unit and currency, and values that cannot be converted are kept as typed and counted as DV-09 errors.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="request">The states and branches of the frontend export.</param>
		/// <returns>The saved diagram with the generated BPMN XML.</returns>
		/// <response code="200">The diagram was overwritten.</response>
		/// <response code="400">The body is invalid, an id is not a valid BPMN identifier or is used twice, a state type is not a BPMN element type, a state has several default branches, or the generated XML exceeds the diagram limits.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="413">The request body exceeds the maximum request size.</response>
		[HttpPut("json")]
		[RequestSizeLimit(AnalysisLimits.MaxSaveRequestBytes)]
		[ProducesResponseType(typeof(ApiResponse<DiagramDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status413PayloadTooLarge)]
		public async Task<IActionResult> SaveJsonDiagram(Guid projectId, [FromBody] ProcessDiagram request) =>
			Success(await diagramService.SaveJsonDiagramAsync(projectId, request));

		/// <summary>Downloads the project's diagram as a .bpmn file named after the project. The body is the raw XML without the response envelope.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <returns>The BPMN XML as a file attachment.</returns>
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
