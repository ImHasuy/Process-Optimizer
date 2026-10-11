using System.ComponentModel.DataAnnotations;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ProcessStateDTO
	{
		[Required(ErrorMessage = "The state id is required.")]
		[StringLength(FieldLimits.BpmnIdMaxLength, ErrorMessage = "The state id must be at most 255 characters long.")]
		public string Id { get; set; } = string.Empty;

		[StringLength(FieldLimits.TextMaxLength, ErrorMessage = "The state name must be at most 1000 characters long.")]
		public string? Name { get; set; }

		[Required(ErrorMessage = "The state type is required.")]
		[StringLength(FieldLimits.BpmnTypeMaxLength, ErrorMessage = "The state type must be at most 100 characters long.")]
		public string Type { get; set; } = string.Empty;

		[StringLength(FieldLimits.MetricTextMaxLength, ErrorMessage = "The time must be at most 100 characters long.")]
		public string? Time { get; set; }

		[StringLength(FieldLimits.MetricTextMaxLength, ErrorMessage = "The price must be at most 100 characters long.")]
		public string? Price { get; set; }
	}
}
