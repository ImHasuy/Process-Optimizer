using System.Net;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerTests.Infrastructure
{
	public static class ApiResponseAssertions
	{
		public static async Task<ApiResponse> AssertApiErrorAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus, string? expectedMessage = null)
		{
			Assert.Equal(expectedStatus, response.StatusCode);
			var body = await response.ReadApiErrorAsync();
			Assert.Equal((int)expectedStatus, body.StatusCode);
			Assert.False(string.IsNullOrWhiteSpace(body.Message));
			if (expectedMessage is not null)
			{
				Assert.Equal(expectedMessage, body.Message);
			}

			return body;
		}

		public static async Task AssertApiErrorContainingAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedMessagePart)
		{
			var body = await response.AssertApiErrorAsync(expectedStatus);
			Assert.Contains(expectedMessagePart, body.Message);
		}

		public static async Task AssertValidationErrorAsync(this HttpResponseMessage response, string field)
		{
			var body = await response.AssertApiErrorAsync(HttpStatusCode.BadRequest);
			Assert.NotNull(body.Errors);
			Assert.Contains(body.Errors.Keys, key => key.EndsWith(field, StringComparison.OrdinalIgnoreCase));
		}
	}
}
