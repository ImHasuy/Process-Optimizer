using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectOptimizerLib.DTO;

namespace ProjectOptimizerLib.Model.Process
{
	/// <summary>A branch (BPMN sequence flow) of the frontend's JSON export.</summary>
	public sealed class ProcessBranch
	{
		[Required(ErrorMessage = "The branch id is required.")]
		[StringLength(FieldLimits.BpmnIdMaxLength, ErrorMessage = "The branch id must be at most 255 characters long.")]
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[StringLength(FieldLimits.TextMaxLength, ErrorMessage = "The branch name must be at most 1000 characters long.")]
		[JsonPropertyName("name")]
		public string? Name { get; set; }

		[Required(ErrorMessage = "The branch source is required.")]
		[StringLength(FieldLimits.BpmnIdMaxLength, ErrorMessage = "The branch source must be at most 255 characters long.")]
		[JsonPropertyName("source")]
		public string Source { get; set; } = string.Empty;

		[Required(ErrorMessage = "The branch target is required.")]
		[StringLength(FieldLimits.BpmnIdMaxLength, ErrorMessage = "The branch target must be at most 255 characters long.")]
		[JsonPropertyName("target")]
		public string Target { get; set; } = string.Empty;

		/// <summary>The logical branch type; see <see cref="ProcessBranchTypes"/>. Unknown values are kept as sent.</summary>
		[StringLength(FieldLimits.BpmnTypeMaxLength, ErrorMessage = "The branch type must be at most 100 characters long.")]
		[JsonPropertyName("type")]
		public string? Type { get; set; }

		[StringLength(FieldLimits.TextMaxLength, ErrorMessage = "The branch condition must be at most 1000 characters long.")]
		[JsonPropertyName("condition")]
		public string? Condition { get; set; }

		/// <summary>Whether this is the default (fallback) flow of its source, marked with the <c>nor</c> type.</summary>
		[JsonIgnore]
		public bool IsDefault => string.Equals(Type?.Trim(), ProcessBranchTypes.Nor, StringComparison.OrdinalIgnoreCase);
	}
}
