using Microsoft.AspNetCore.Mvc;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Controllers
{
	[Route("api/health")]
	public sealed class HealthController : ApiControllerBase
	{
		private const string RunningStatus = "running";
		private const string RunningMessage = "API is working";

		/// <summary>Reports that the API process is running.</summary>
		/// <returns>The running status of the API.</returns>
		/// <response code="200">The API is running.</response>
		[HttpGet]
		[ProducesResponseType(typeof(ApiResponse<HealthDTO>), StatusCodes.Status200OK)]
		public IActionResult GetHealth() => Success(new HealthDTO { Status = RunningStatus, Message = RunningMessage });
	}
}
