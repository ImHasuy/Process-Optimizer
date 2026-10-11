using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IDiagramRevisionFactory
	{
		DiagramRevision Create(Guid projectId, int revisionNumber, string bpmnXml, DateTime createdAt);
	}
}
