using System.ComponentModel.DataAnnotations;

namespace ProjectOptimizerLib.DTO
{
	/// <summary>Limits the length of a string after its leading and trailing white space is removed.</summary>
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class TrimmedStringLengthAttribute(int maximumLength) : ValidationAttribute
	{
		public int MaximumLength { get; } = maximumLength;

		public override bool IsValid(object? value) => value is not string text || text.Trim().Length <= MaximumLength;
	}
}
