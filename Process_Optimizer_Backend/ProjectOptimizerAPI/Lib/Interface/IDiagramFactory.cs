using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IDiagramFactory
	{
		Diagram Create(Guid projectId, string bpmnXml, DateTime updatedAt);
		void Apply(Diagram diagram, string bpmnXml, DateTime updatedAt);
	}
}
