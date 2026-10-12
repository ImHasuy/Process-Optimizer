using System.ComponentModel.DataAnnotations;

namespace ProjectOptimizerLib.DTO
{
	public sealed class DiagramSaveDTO
	{
		[Required(ErrorMessage = "The diagram XML is required.")]
		public string BpmnXml { get; set; } = string.Empty;
	}
}
