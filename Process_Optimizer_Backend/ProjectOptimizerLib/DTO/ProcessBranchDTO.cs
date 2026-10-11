using System.ComponentModel.DataAnnotations;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ProcessBranchDTO
	{
		[Required(ErrorMessage = "The branch id is required.")]
		[StringLength(FieldLimits.BpmnIdMaxLength, ErrorMessage = "The branch id must be at most 255 characters long.")]
		public string Id { get; set; } = string.Empty;

		[StringLength(FieldLimits.TextMaxLength, ErrorMessage = "The branch name must be at most 1000 characters long.")]
		public string? Name { get; set; }

		[Required(ErrorMessage = "The branch source is required.")]
		[StringLength(FieldLimits.BpmnIdMaxLength, ErrorMessage = "The branch source must be at most 255 characters long.")]
		public string Source { get; set; } = string.Empty;

		[Required(ErrorMessage = "The branch target is required.")]
		[StringLength(FieldLimits.BpmnIdMaxLength, ErrorMessage = "The branch target must be at most 255 characters long.")]
		public string Target { get; set; } = string.Empty;

		[StringLength(FieldLimits.BpmnTypeMaxLength, ErrorMessage = "The branch type must be at most 100 characters long.")]
		public string? Type { get; set; }

		[StringLength(FieldLimits.TextMaxLength, ErrorMessage = "The branch condition must be at most 1000 characters long.")]
		public string? Condition { get; set; }
	}
}
