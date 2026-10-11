using System.ComponentModel.DataAnnotations;

namespace ProjectOptimizerLib.DTO
{
	public sealed class RevisionQueryDTO
	{
		[Range(1, int.MaxValue, ErrorMessage = "The revision must be at least 1.")]
		public int? Revision { get; set; }
	}
}
