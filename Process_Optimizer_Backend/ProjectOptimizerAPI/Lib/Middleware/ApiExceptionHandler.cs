using Microsoft.AspNetCore.Diagnostics;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Middleware
{
	public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
	{
		public const string RequestTooLargeMessage = "The request body is too large.";
		public const string MalformedRequestMessage = "The request is malformed.";

		public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
		{
			var response = CreateResponse(exception);

			LogException(exception, response.StatusCode);

			httpContext.Response.StatusCode = response.StatusCode;
			await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
			return true;
		}

		private static ApiResponse CreateResponse(Exception exception) => exception switch
		{
			ApiException apiException => ApiResponseFactory.Error(apiException.StatusCode, apiException.Message),
			BadHttpRequestException badRequest => ApiResponseFactory.Error(badRequest.StatusCode, BadRequestMessage(badRequest.StatusCode)),
			_ => ApiResponseFactory.Error(StatusCodes.Status500InternalServerError, ApiResponseFactory.UnexpectedErrorMessage)
		};

		private static string BadRequestMessage(int statusCode) =>
			statusCode == StatusCodes.Status413PayloadTooLarge ? RequestTooLargeMessage : MalformedRequestMessage;

		private void LogException(Exception exception, int statusCode)
		{
			if (statusCode >= StatusCodes.Status500InternalServerError)
			{
				logger.LogError(exception, "Unhandled exception while processing the request.");
				return;
			}

			logger.LogWarning("Request rejected with {StatusCode}: {Message}", statusCode, exception.Message);
		}
	}
}
