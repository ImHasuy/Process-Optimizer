using System.ComponentModel.DataAnnotations;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	/// <summary>Full replacement of the project settings. Changing TimeUnit or Currency does not convert the numbers stored in the diagram (BR-23).</summary>
	public sealed class ProjectUpdateDTO
	{
		[Required(ErrorMessage = "The project name is required.")]
		[TrimmedStringLength(FieldLimits.NameMaxLength, ErrorMessage = "The project name must be at most 100 characters long.")]
		public string Name { get; set; } = string.Empty;

		[StringLength(FieldLimits.TextMaxLength, ErrorMessage = "The description must be at most 1000 characters long.")]
		public string? Description { get; set; }

		[Required(ErrorMessage = "The time unit is required.")]
		[EnumDataType(typeof(ETimeUnit), ErrorMessage = "The time unit must be Minutes, Hours, Days or Weeks.")]
		public ETimeUnit? TimeUnit { get; set; }

		[Required(ErrorMessage = "The currency is required.")]
		[RegularExpression("^[A-Z]{3}$", ErrorMessage = "The currency must be a three-letter upper-case ISO 4217 code.")]
		public string? Currency { get; set; }
	}
}
