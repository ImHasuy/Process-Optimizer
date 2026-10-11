using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IResourceLimitCalculator
	{
		ResourceLimitsDTO Calculate(ProcessGraph graph, StructureFragment root, EResourceLimitBasis basis, RevisionContext context);
	}
}
