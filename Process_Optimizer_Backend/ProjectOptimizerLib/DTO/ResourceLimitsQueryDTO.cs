using System.ComponentModel.DataAnnotations;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ResourceLimitsQueryDTO
	{
		[EnumDataType(typeof(EResourceLimitBasis), ErrorMessage = "The basis must be Planned or Pessimistic.")]
		public EResourceLimitBasis? Basis { get; set; }
	}
}
