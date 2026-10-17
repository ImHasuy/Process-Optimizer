using ProjectOptimizerLib.Enum;
using ProjectOptimizerLib.Model.Process;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IProcessJsonConverter
	{
		string ToBpmnXml(ProcessDiagram diagram, ETimeUnit timeUnit, string currency);
	}
}
