using Microsoft.AspNetCore.Mvc;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Controllers
{
	[Route("api/projects")]
	public sealed class ProjectController(IProjectService projectService) : ApiControllerBase
	{
		/// <summary>Lists all projects, the most recently updated first.</summary>
		/// <returns>The projects with the summary of their diagram.</returns>
		/// <response code="200">The list of projects, possibly empty.</response>
		[HttpGet]
		[ProducesResponseType(typeof(ApiResponse<List<ProjectListItemDTO>>), StatusCodes.Status200OK)]
		public async Task<IActionResult> ListProjects() => Success(await projectService.ListProjectsAsync());

		/// <summary>Creates a project with the default diagram.</summary>
		/// <param name="request">The name (required, unique ignoring case and surrounding spaces), optional description, time unit (default Days) and currency (default EUR).</param>
		/// <returns>The created project with its diagram summary.</returns>
		/// <response code="201">The project was created; the Location header points to it.</response>
		/// <response code="400">The request body is invalid.</response>
		/// <response code="409">A project with this name already exists.</response>
		[HttpPost]
		[ProducesResponseType(typeof(ApiResponse<ProjectDetailsDTO>), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> CreateProject([FromBody] ProjectCreateDTO request)
		{
			var project = await projectService.CreateProjectAsync(request);
			return Created(nameof(GetProject), new { id = project.Id }, project);
		}

		/// <summary>Returns one project with the summary of its diagram.</summary>
		/// <param name="id">The identifier of the project.</param>
		/// <returns>The requested project.</returns>
		/// <response code="200">The project was found.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		[HttpGet("{id:guid}")]
		[ProducesResponseType(typeof(ApiResponse<ProjectDetailsDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetProject(Guid id) => Success(await projectService.GetProjectAsync(id));

		/// <summary>Replaces the settings of a project. Changing the time unit or currency does not convert the numbers stored in the diagram.</summary>
		/// <param name="id">The identifier of the project.</param>
		/// <param name="request">The new name, description (null clears it), time unit and currency.</param>
		/// <returns>No content.</returns>
		/// <response code="204">The project was updated.</response>
		/// <response code="400">The request body is invalid.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="409">Another project already has this name.</response>
		[HttpPut("{id:guid}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> UpdateProject(Guid id, [FromBody] ProjectUpdateDTO request)
		{
			await projectService.UpdateProjectAsync(id, request);
			return NoContent();
		}

		/// <summary>Deletes a project together with its diagram and saved scenarios.</summary>
		/// <param name="id">The identifier of the project.</param>
		/// <returns>No content.</returns>
		/// <response code="204">The project was deleted.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		[HttpDelete("{id:guid}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> DeleteProject(Guid id)
		{
			await projectService.DeleteProjectAsync(id);
			return NoContent();
		}
	}
}
