using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IProcessJsonConverter
	{
		string ToBpmnXml(IReadOnlyList<ProcessStateDTO> states, IReadOnlyList<ProcessBranchDTO> branches, ETimeUnit timeUnit, string currency);
	}
}
