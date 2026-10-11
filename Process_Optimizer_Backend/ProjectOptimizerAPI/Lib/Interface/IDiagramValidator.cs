using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IDiagramValidator
	{
		DiagramValidationResult Validate(ProcessGraph graph);
	}
}
