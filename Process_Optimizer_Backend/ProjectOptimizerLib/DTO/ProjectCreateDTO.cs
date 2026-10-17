using System.ComponentModel.DataAnnotations;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ProjectCreateDTO
	{
		[Required(ErrorMessage = "The project name is required.")]
		[TrimmedStringLength(FieldLimits.NameMaxLength, ErrorMessage = "The project name must be at most 100 characters long.")]
		public string Name { get; set; } = string.Empty;

		[StringLength(FieldLimits.TextMaxLength, ErrorMessage = "The description must be at most 1000 characters long.")]
		public string? Description { get; set; }

		[EnumDataType(typeof(ETimeUnit), ErrorMessage = "The time unit must be Minutes, Hours, Days or Weeks.")]
		public ETimeUnit? TimeUnit { get; set; }

		[RegularExpression("^[A-Z]{3}$", ErrorMessage = "The currency must be a three-letter upper-case ISO 4217 code.")]
		public string? Currency { get; set; }
	}
}
