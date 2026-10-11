using System.ComponentModel.DataAnnotations;

namespace ProjectOptimizerLib.DTO
{
	public sealed class DiagramRevisionCreateDTO
	{
		[Required(ErrorMessage = "The diagram XML is required.")]
		public string BpmnXml { get; set; } = string.Empty;

		[Required(ErrorMessage = "The base revision is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "The base revision must be at least 1.")]
		public int? BaseRevision { get; set; }
	}
}
