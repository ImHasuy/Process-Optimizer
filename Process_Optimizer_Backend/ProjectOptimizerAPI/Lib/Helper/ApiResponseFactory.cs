using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Helper
{
	public static class ApiResponseFactory
	{
		public const string SuccessMessage = "Success";
		public const string ValidationFailedMessage = "One or more validation errors occurred.";
		public const string UnexpectedErrorMessage = "An unexpected error occurred.";

		public static ApiResponse<TData> Success<TData>(TData data, int statusCode = StatusCodes.Status200OK) =>
			new() { StatusCode = statusCode, Message = SuccessMessage, Data = data };

		public static ApiResponse Error(int statusCode, string message) =>
			new() { StatusCode = statusCode, Message = message };

		public static ApiResponse ValidationError(ModelStateDictionary modelState) =>
			new()
			{
				StatusCode = StatusCodes.Status400BadRequest,
				Message = ValidationFailedMessage,
				Errors = modelState
					.Where(entry => entry.Value is { Errors.Count: > 0 })
					.ToDictionary(
						entry => entry.Key,
						entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray())
			};
	}
}
