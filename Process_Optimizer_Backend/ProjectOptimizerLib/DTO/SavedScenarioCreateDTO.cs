using System.ComponentModel.DataAnnotations;

namespace ProjectOptimizerLib.DTO
{
	public sealed class SavedScenarioCreateDTO
	{
		[Required(ErrorMessage = "The scenario index is required.")]
		[Range(typeof(long), "0", "9223372036854775807", ErrorMessage = "The scenario index must be zero or greater.")]
		public long? ScenarioIndex { get; set; }

		[Required(ErrorMessage = "The saved scenario name is required.")]
		[TrimmedStringLength(FieldLimits.NameMaxLength, ErrorMessage = "The saved scenario name must be at most 100 characters long.")]
		public string Name { get; set; } = string.Empty;

		[StringLength(FieldLimits.TextMaxLength, ErrorMessage = "The note must be at most 1000 characters long.")]
		public string? Note { get; set; }
	}
}
