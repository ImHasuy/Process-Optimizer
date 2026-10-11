using System.ComponentModel.DataAnnotations;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ScenarioFilterDTO
	{
		[Range(1, int.MaxValue, ErrorMessage = "The revision must be at least 1.")]
		public int? Revision { get; set; }

		[Range(1, int.MaxValue, ErrorMessage = "The page must be at least 1.")]
		public int? Page { get; set; }

		[Range(1, FieldLimits.MaxPageSize, ErrorMessage = "The page size must be between 1 and 100.")]
		public int? PageSize { get; set; }

		[EnumDataType(typeof(EScenarioSortBy), ErrorMessage = "The sort field must be Index, TotalCost or TotalDuration.")]
		public EScenarioSortBy? SortBy { get; set; }

		[EnumDataType(typeof(ESortDirection), ErrorMessage = "The sort direction must be Asc or Desc.")]
		public ESortDirection? SortDirection { get; set; }
	}
}
