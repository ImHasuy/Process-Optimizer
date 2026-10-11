using System.ComponentModel.DataAnnotations;

namespace ProjectOptimizerLib.DTO
{
	public sealed class SavedScenarioUpdateDTO
	{
		[Required(ErrorMessage = "The saved scenario name is required.")]
		[TrimmedStringLength(FieldLimits.NameMaxLength, ErrorMessage = "The saved scenario name must be at most 100 characters long.")]
		public string Name { get; set; } = string.Empty;

		[StringLength(FieldLimits.TextMaxLength, ErrorMessage = "The note must be at most 1000 characters long.")]
		public string? Note { get; set; }
	}
}
