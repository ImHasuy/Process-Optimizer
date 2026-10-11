using System.ComponentModel.DataAnnotations;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerLib.DTO
{
	public sealed class ProjectFilterDTO
	{
		[StringLength(FieldLimits.SearchTermMaxLength, ErrorMessage = "The search term must be at most 100 characters long.")]
		public string? Search { get; set; }

		[EnumDataType(typeof(EProjectSortBy), ErrorMessage = "The sort field must be Name, CreatedAt or UpdatedAt.")]
		public EProjectSortBy? SortBy { get; set; }

		[EnumDataType(typeof(ESortDirection), ErrorMessage = "The sort direction must be Asc or Desc.")]
		public ESortDirection? SortDirection { get; set; }

		[Range(1, int.MaxValue, ErrorMessage = "The page must be at least 1.")]
		public int? Page { get; set; }

		[Range(1, FieldLimits.MaxPageSize, ErrorMessage = "The page size must be between 1 and 100.")]
		public int? PageSize { get; set; }
	}
}
