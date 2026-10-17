using Microsoft.AspNetCore.Mvc;
using ProjectOptimizerAPI.Lib.Helper;

namespace ProjectOptimizerAPI.Controllers
{
	[ApiController]
	[Produces("application/json")]
	public abstract class ApiControllerBase : ControllerBase
	{
		protected OkObjectResult Success<TData>(TData data) => Ok(ApiResponseFactory.Success(data));

		protected CreatedAtActionResult Created<TData>(string actionName, object routeValues, TData data) =>
			CreatedAtAction(actionName, routeValues, ApiResponseFactory.Success(data, StatusCodes.Status201Created));
	}
}
