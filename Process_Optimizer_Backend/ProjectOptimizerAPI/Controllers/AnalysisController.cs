using Microsoft.AspNetCore.Mvc;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Controllers
{
	[Route("api/projects/{projectId:guid}")]
	public sealed class AnalysisController(IAnalysisService analysisService) : ApiControllerBase
	{
		/// <summary>Returns the parsed process graph of a diagram revision, even when the diagram has validation errors.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="query">The optional revision number; the latest revision is used when omitted.</param>
		/// <returns>The flow nodes with their dependencies and the sequence flows of the revision.</returns>
		/// <response code="200">The process graph of the requested revision.</response>
		/// <response code="400">The revision is less than 1 or does not exist for the project.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		[HttpGet("graph")]
		[ProducesResponseType(typeof(ApiResponse<ProcessGraphDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetGraph(Guid projectId, [FromQuery] RevisionQueryDTO query) =>
			Success(await analysisService.GetGraphAsync(projectId, query.Revision));

		/// <summary>Returns the validation report of a diagram revision.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="query">The optional revision number; the latest revision is used when omitted.</param>
		/// <returns>The validation issues of the revision and whether it can be optimized.</returns>
		/// <response code="200">The validation report of the requested revision.</response>
		/// <response code="400">The revision is less than 1 or does not exist for the project.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		[HttpGet("validation")]
		[ProducesResponseType(typeof(ApiResponse<ValidationReportDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetValidationReport(Guid projectId, [FromQuery] RevisionQueryDTO query) =>
			Success(await analysisService.GetValidationReportAsync(projectId, query.Revision));
	}
}
