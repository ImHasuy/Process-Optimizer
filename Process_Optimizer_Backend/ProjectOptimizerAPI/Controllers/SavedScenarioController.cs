using Microsoft.AspNetCore.Mvc;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Controllers
{
	[Route("api/projects/{projectId:guid}/saved-scenarios")]
	public sealed class SavedScenarioController(ISavedScenarioService savedScenarioService) : ApiControllerBase
	{
		/// <summary>Saves a snapshot of one scenario of the project's diagram under a name unique within the project.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="request">The scenario index, the name and an optional note.</param>
		/// <returns>The saved scenario with its frozen snapshot.</returns>
		/// <response code="201">The scenario was saved.</response>
		/// <response code="400">The request body is invalid or the scenario index is not below the scenario count.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="409">The name is already used by another saved scenario of the project, or the diagram cannot be enumerated into scenarios.</response>
		[HttpPost]
		[ProducesResponseType(typeof(ApiResponse<SavedScenarioDTO>), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> CreateSavedScenario(Guid projectId, [FromBody] SavedScenarioCreateDTO request)
		{
			var saved = await savedScenarioService.CreateSavedScenarioAsync(projectId, request);
			return Created(nameof(GetSavedScenario), new { projectId, id = saved.Id }, saved);
		}

		/// <summary>Lists the saved scenarios of a project, newest first, with whether they still exist in the current diagram.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <returns>The saved scenarios of the project.</returns>
		/// <response code="200">The list of saved scenarios, possibly empty.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		[HttpGet]
		[ProducesResponseType(typeof(ApiResponse<List<SavedScenarioListItemDTO>>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> ListSavedScenarios(Guid projectId) =>
			Success(await savedScenarioService.ListSavedScenariosAsync(projectId));

		/// <summary>Returns one saved scenario with its unchanged snapshot and its index in the current diagram.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="id">The identifier of the saved scenario.</param>
		/// <returns>The requested saved scenario.</returns>
		/// <response code="200">The saved scenario was found.</response>
		/// <response code="404">No project exists with the given identifier, or the project has no saved scenario with the given identifier.</response>
		[HttpGet("{id:guid}")]
		[ProducesResponseType(typeof(ApiResponse<SavedScenarioDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetSavedScenario(Guid projectId, Guid id) =>
			Success(await savedScenarioService.GetSavedScenarioAsync(projectId, id));

		/// <summary>Renames a saved scenario and replaces its note; the snapshot never changes.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="id">The identifier of the saved scenario.</param>
		/// <param name="request">The new name and note.</param>
		/// <returns>No content.</returns>
		/// <response code="204">The saved scenario was updated.</response>
		/// <response code="400">The request body is invalid.</response>
		/// <response code="404">No project exists with the given identifier, or the project has no saved scenario with the given identifier.</response>
		/// <response code="409">The name is already used by another saved scenario of the project.</response>
		[HttpPut("{id:guid}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> UpdateSavedScenario(Guid projectId, Guid id, [FromBody] SavedScenarioUpdateDTO request)
		{
			await savedScenarioService.UpdateSavedScenarioAsync(projectId, id, request);
			return NoContent();
		}

		/// <summary>Deletes a saved scenario.</summary>
		/// <param name="projectId">The identifier of the project.</param>
		/// <param name="id">The identifier of the saved scenario.</param>
		/// <returns>No content.</returns>
		/// <response code="204">The saved scenario was deleted.</response>
		/// <response code="404">No project exists with the given identifier, or the project has no saved scenario with the given identifier.</response>
		[HttpDelete("{id:guid}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> DeleteSavedScenario(Guid projectId, Guid id)
		{
			await savedScenarioService.DeleteSavedScenarioAsync(projectId, id);
			return NoContent();
		}
	}
}
