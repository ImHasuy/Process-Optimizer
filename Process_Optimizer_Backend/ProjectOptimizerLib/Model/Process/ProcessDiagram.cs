using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectOptimizerLib.Model.Process
{
	/// <summary>The process diagram in the frontend's JSON export format: <c>{ "states": [...], "branches": [...] }</c>.</summary>
	public sealed class ProcessDiagram
	{
		private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

		private List<ProcessState> _states = [];
		private List<ProcessBranch> _branches = [];

		[JsonPropertyName("states")]
		public List<ProcessState> States { get => _states; set => _states = value ?? []; }

		[JsonPropertyName("branches")]
		public List<ProcessBranch> Branches { get => _branches; set => _branches = value ?? []; }

		public static ProcessDiagram Parse(string json) =>
			JsonSerializer.Deserialize<ProcessDiagram>(json, SerializerOptions) ?? throw new JsonException("The process diagram JSON is empty.");

		public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);
	}
}
