using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerAPI.Lib.Helper
{
	public static class ScenarioSnapshotSerializer
	{
		private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
		{
			Converters = { new JsonStringEnumConverter() }
		};

		public static string Serialize(ScenarioDTO scenario) => JsonSerializer.Serialize(scenario, Options);

		public static ScenarioDTO Deserialize(string json) =>
			JsonSerializer.Deserialize<ScenarioDTO>(json, Options) ?? throw new InvalidOperationException("The saved scenario snapshot is empty.");
	}
}
