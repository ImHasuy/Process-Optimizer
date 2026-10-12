using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Controllers
{
	[Route("api/projects/{projectId:guid}")]
	public sealed class ScenarioController(IScenarioService scenarioService) : ApiControllerBase
	{
		/// <summary>Returns the number of scenarios of the project's diagram without building any of them.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <returns>The scenario ordering version and the scenario count.</returns>
		/// <response code="200">The scenario count.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="409">The diagram has errors or too many scenarios to index.</response>
		[HttpGet("scenario-count")]
		[ProducesResponseType(typeof(ApiResponse<ScenarioCountDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> GetScenarioCount(Guid projectId) =>
			Success(await scenarioService.GetScenarioCountAsync(projectId));

		/// <summary>Returns every scenario of the project's diagram in index order, each with its activities, critical path and totals.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <returns>All scenarios, the same shape as a single scenario.</returns>
		/// <response code="200">All scenarios.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="409">The diagram has errors, too many scenarios to index, or more than 1000 scenarios (request them one by one).</response>
		[HttpGet("scenarios")]
		[ProducesResponseType(typeof(ApiResponse<List<ScenarioDTO>>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> GetAllScenarios(Guid projectId) =>
			Success(await scenarioService.GetAllScenariosAsync(projectId));

		/// <summary>Returns one scenario of the project's diagram by its canonical index.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="index">The zero-based canonical index of the scenario.</param>
		/// <returns>The scenario with its activities, critical path and totals.</returns>
		/// <response code="200">The requested scenario.</response>
		/// <response code="400">The index is negative.</response>
		/// <response code="404">No project exists with the given identifier or the scenario index is out of range.</response>
		/// <response code="409">The diagram has errors or too many scenarios to index.</response>
		[HttpGet("scenarios/{index:long}")]
		[ProducesResponseType(typeof(ApiResponse<ScenarioDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> GetScenario(
			Guid projectId,
			[Range(typeof(long), "0", "9223372036854775807", ErrorMessage = "The scenario index must be zero or greater.")] long index) =>
			Success(await scenarioService.GetScenarioAsync(projectId, index));

		/// <summary>Returns the cost and duration limits over every scenario of the project's diagram.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="query">The estimate basis (Planned or Pessimistic, default Planned).</param>
		/// <returns>The resource limits and the scenarios that reach them.</returns>
		/// <response code="200">The resource limits.</response>
		/// <response code="400">A query value is invalid.</response>
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
