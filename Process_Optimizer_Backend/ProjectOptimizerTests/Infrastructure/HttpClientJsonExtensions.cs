using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerTests.Infrastructure
{
	public static class HttpClientJsonExtensions
	{
		private const string ProjectsUrl = "/api/projects";

		private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
		{
			Converters = { new JsonStringEnumConverter() }
		};

		public static async Task<ApiResponse<TData>> ReadApiResponseAsync<TData>(this HttpResponseMessage response) =>
			(await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(SerializerOptions))!;

		public static async Task<ApiResponse> ReadApiErrorAsync(this HttpResponseMessage response) =>
			(await response.Content.ReadFromJsonAsync<ApiResponse>(SerializerOptions))!;

		public static async Task<TData> PostAndReadDataAsync<TData>(this HttpClient client, string url, object body)
		{
			var response = await client.PostAsJsonAsync(url, body, SerializerOptions);
			response.EnsureSuccessStatusCode();
			return (await response.ReadApiResponseAsync<TData>()).Data!;
		}

		public static Task<ProjectDetailsDTO> CreateProjectAsync(this HttpClient client, string? name = null) =>
			client.PostAndReadDataAsync<ProjectDetailsDTO>(ProjectsUrl, new { Name = name ?? $"Project {Guid.NewGuid():N}" });

		public static async Task<Guid> CreateProjectWithDiagramAsync(this HttpClient client, string bpmnXml)
		{
			var project = await client.CreateProjectAsync();
			await client.SaveDiagramAsync(project.Id, bpmnXml);
			return project.Id;
		}

		public static async Task<DiagramDTO> SaveDiagramAsync(this HttpClient client, Guid projectId, string bpmnXml)
		{
			var response = await client.PutAsJsonAsync($"{ProjectsUrl}/{projectId}/diagram", new { BpmnXml = bpmnXml }, SerializerOptions);
			response.EnsureSuccessStatusCode();
			return (await response.ReadApiResponseAsync<DiagramDTO>()).Data!;
		}
	}
}
