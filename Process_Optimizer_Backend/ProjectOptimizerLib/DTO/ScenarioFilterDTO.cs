using System.ComponentModel.DataAnnotations;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ScenarioFilterDTO
	{
		[Range(1, int.MaxValue, ErrorMessage = "The page must be at least 1.")]
		public int? Page { get; set; }

		[Range(1, FieldLimits.MaxPageSize, ErrorMessage = "The page size must be between 1 and 100.")]
		public int? PageSize { get; set; }
	}
}
