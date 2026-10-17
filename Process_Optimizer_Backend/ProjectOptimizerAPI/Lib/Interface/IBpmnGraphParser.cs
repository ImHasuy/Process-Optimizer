using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IBpmnGraphParser
	{
		ProcessGraph Parse(string bpmnXml);
	}
}
