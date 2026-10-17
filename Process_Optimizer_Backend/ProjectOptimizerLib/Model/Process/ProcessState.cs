using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerLib.Model.Process
{
	/// <summary>A state (event, activity or gateway) of the frontend's JSON export.</summary>
	public sealed class ProcessState
	{
		[Required(ErrorMessage = "The state id is required.")]
		[StringLength(FieldLimits.BpmnIdMaxLength, ErrorMessage = "The state id must be at most 255 characters long.")]
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[StringLength(FieldLimits.TextMaxLength, ErrorMessage = "The state name must be at most 1000 characters long.")]
		[JsonPropertyName("name")]
		public string? Name { get; set; }

		/// <summary>The BPMN element type, such as <c>bpmn:Task</c> or <c>bpmn:ExclusiveGateway</c>.</summary>
		[Required(ErrorMessage = "The state type is required.")]
		[StringLength(FieldLimits.BpmnTypeMaxLength, ErrorMessage = "The state type must be at most 100 characters long.")]
		[JsonPropertyName("type")]
		public string Type { get; set; } = string.Empty;

		/// <summary>The duration as typed in the editor, such as <c>15m</c>, <c>2h 30m</c> or <c>3</c>.</summary>
		[StringLength(FieldLimits.MetricTextMaxLength, ErrorMessage = "The time must be at most 100 characters long.")]
		[JsonPropertyName("time")]
		public string? Time { get; set; }

		/// <summary>The cost as typed in the editor, such as <c>25 EUR</c>, <c>$10</c> or <c>100</c>.</summary>
		[StringLength(FieldLimits.MetricTextMaxLength, ErrorMessage = "The price must be at most 100 characters long.")]
		[JsonPropertyName("price")]
		public string? Price { get; set; }

		/// <summary>The ids of the states with a branch into this state.</summary>
		[JsonPropertyName("Depends_on")]
		public List<string> DependsOn { get; set; } = [];

		/// <summary>The ids of the states this state has a branch to.</summary>
		[JsonPropertyName("Connects_to")]
		public List<string> ConnectsTo { get; set; } = [];
	}
}
