using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Controllers
{
	[Route("api/projects/{projectId:guid}")]
	public sealed class ScenarioController(IScenarioService scenarioService) : ApiControllerBase
	{
		/// <summary>Returns the number of scenarios of a diagram revision without building any of them.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="query">The optional revision number; the latest revision is used when omitted.</param>
		/// <returns>The revision number, the scenario ordering version and the scenario count.</returns>
		/// <response code="200">The scenario count of the revision.</response>
		/// <response code="400">The revision number is invalid or does not exist.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="409">The diagram has errors or too many scenarios to index.</response>
		[HttpGet("scenario-count")]
		[ProducesResponseType(typeof(ApiResponse<ScenarioCountDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> GetScenarioCount(Guid projectId, [FromQuery] RevisionQueryDTO query) =>
			Success(await scenarioService.GetScenarioCountAsync(projectId, query.Revision));

		/// <summary>Lists one page of the scenarios of a diagram revision.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="filter">The optional revision, page, page size, sort field and sort direction.</param>
		/// <returns>One page of scenario summaries and the total scenario count.</returns>
		/// <response code="200">The requested page of scenarios, possibly empty.</response>
		/// <response code="400">A query value is invalid, the revision does not exist or there are too many scenarios to sort.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="409">The diagram has errors or too many scenarios to index.</response>
		[HttpGet("scenarios")]
		[ProducesResponseType(typeof(ApiResponse<PagedResultDTO<ScenarioListItemDTO>>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> ListScenarios(Guid projectId, [FromQuery] ScenarioFilterDTO filter) =>
			Success(await scenarioService.ListScenariosAsync(projectId, filter));

		/// <summary>Returns one scenario of a diagram revision by its canonical index.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="index">The zero-based canonical index of the scenario.</param>
		/// <param name="query">The optional revision number; the latest revision is used when omitted.</param>
		/// <returns>The scenario with its activities, critical path and totals.</returns>
		/// <response code="200">The requested scenario.</response>
		/// <response code="400">The index or revision number is invalid, or the revision does not exist.</response>
		/// <response code="404">No project exists with the given identifier or the scenario index is out of range.</response>
		/// <response code="409">The diagram has errors or too many scenarios to index.</response>
		[HttpGet("scenarios/{index:long}")]
		[ProducesResponseType(typeof(ApiResponse<ScenarioDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> GetScenario(
			Guid projectId,
			[Range(typeof(long), "0", "9223372036854775807", ErrorMessage = "The scenario index must be zero or greater.")] long index,
			[FromQuery] RevisionQueryDTO query) =>
			Success(await scenarioService.GetScenarioAsync(projectId, index, query.Revision));

		/// <summary>Returns the cost and duration limits over every scenario of a diagram revision.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="query">The optional revision number and estimate basis (Planned or Pessimistic).</param>
		/// <returns>The resource limits and the scenarios that reach them.</returns>
		/// <response code="200">The resource limits of the revision.</response>
		/// <response code="400">A query value is invalid or the revision does not exist.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="409">The diagram has errors and cannot be analysed.</response>
		[HttpGet("resource-limits")]
		[ProducesResponseType(typeof(ApiResponse<ResourceLimitsDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> GetResourceLimits(Guid projectId, [FromQuery] ResourceLimitsQueryDTO query) =>
			Success(await scenarioService.GetResourceLimitsAsync(projectId, query));
	}
}
