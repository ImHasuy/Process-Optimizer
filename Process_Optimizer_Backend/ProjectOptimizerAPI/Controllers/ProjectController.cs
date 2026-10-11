using Microsoft.AspNetCore.Mvc;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Controllers
{
	[Route("api/projects")]
	public sealed class ProjectController(IProjectService projectService) : ApiControllerBase
	{
		/// <summary>Lists projects with optional case-insensitive name search, sorting and paging.</summary>
		/// <param name="filter">Search term, sort field (Name, CreatedAt, UpdatedAt), sort direction (Asc, Desc), page (default 1) and page size (default 20, at most 100). Without sort parameters the newest updated project comes first.</param>
		/// <returns>One page of projects with the total number of matching projects.</returns>
		/// <response code="200">The requested page of projects, possibly empty.</response>
		/// <response code="400">A query parameter is invalid (unknown sort field or direction, page below 1, page size outside 1-100, search term longer than 100 characters).</response>
		[HttpGet]
		[ProducesResponseType(typeof(ApiResponse<PagedResultDTO<ProjectListItemDTO>>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
		public async Task<IActionResult> ListProjects([FromQuery] ProjectFilterDTO filter) => Success(await projectService.ListProjectsAsync(filter));

		/// <summary>Creates a project together with revision 1 holding the default diagram.</summary>
		/// <param name="request">The name (required, unique ignoring case and surrounding spaces), optional description, time unit (default Days) and currency (default EUR).</param>
		/// <returns>The created project with its latest revision summary.</returns>
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

		/// <summary>Returns one project with the summary of its latest diagram revision.</summary>
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

		/// <summary>Deletes a project together with all its diagram revisions and saved scenarios.</summary>
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

		/// <summary>Copies a project: the copy is named "&lt;name&gt; (copy)" (then " (copy) 2", " (copy) 3", ...) and gets the latest diagram of the source as its revision 1.</summary>
		/// <param name="id">The identifier of the project to copy.</param>
		/// <returns>The created copy with its latest revision summary.</returns>
		/// <response code="201">The copy was created; the Location header points to it.</response>
		/// <response code="404">No project exists with the given identifier.</response>
		/// <response code="409">The copy name was taken concurrently.</response>
		[HttpPost("{id:guid}/copies")]
		[ProducesResponseType(typeof(ApiResponse<ProjectDetailsDTO>), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> CopyProject(Guid id)
		{
			var copy = await projectService.CopyProjectAsync(id);
			return Created(nameof(GetProject), new { id = copy.Id }, copy);
		}
	}
}
