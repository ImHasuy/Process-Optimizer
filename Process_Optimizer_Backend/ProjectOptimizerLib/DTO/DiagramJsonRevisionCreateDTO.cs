using System.ComponentModel.DataAnnotations;
using ProjectOptimizerLib.Model.Process;

namespace ProjectOptimizerLib.DTO
{
	public sealed class DiagramJsonRevisionCreateDTO
	{
		[Required(ErrorMessage = "The states are required.")]
		public List<ProcessState>? States { get; set; }

		[Required(ErrorMessage = "The branches are required.")]
		public List<ProcessBranch>? Branches { get; set; }

		[Required(ErrorMessage = "The base revision is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "The base revision must be at least 1.")]
		public int? BaseRevision { get; set; }

		public ProcessDiagram ToDiagram() => new() { States = States ?? [], Branches = Branches ?? [] };
	}
}
