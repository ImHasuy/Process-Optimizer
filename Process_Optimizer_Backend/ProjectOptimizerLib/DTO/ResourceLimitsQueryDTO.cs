using System.ComponentModel.DataAnnotations;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ResourceLimitsQueryDTO
	{
		[Range(1, int.MaxValue, ErrorMessage = "The revision must be at least 1.")]
		public int? Revision { get; set; }

		[EnumDataType(typeof(EResourceLimitBasis), ErrorMessage = "The basis must be Planned or Pessimistic.")]
		public EResourceLimitBasis? Basis { get; set; }
	}
}
