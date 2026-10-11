using System.Net;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Integration.Controller
{
	public sealed class HealthControllerTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
	{
		private readonly HttpClient _client = factory.CreateClient();

		[Fact]
		public async Task GetHealth_ReturnsRunning()
		{
			var response = await _client.GetAsync("/api/health");

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.ReadApiResponseAsync<HealthDTO>();
			Assert.Equal((int)HttpStatusCode.OK, body.StatusCode);
			Assert.Equal("running", body.Data!.Status);
		}

		[Fact]
		public async Task SwaggerDocument_ReturnsOk()
		{
			var response = await _client.GetAsync("/swagger/v1/swagger.json");

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		}
	}
}
