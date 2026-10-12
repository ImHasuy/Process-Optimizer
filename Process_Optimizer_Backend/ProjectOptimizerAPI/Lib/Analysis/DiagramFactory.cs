using System.Text;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed class DiagramFactory(IDiagramAnalyzer diagramAnalyzer, IResourceLimitCalculator resourceLimitCalculator) : IDiagramFactory
	{
		public Diagram Create(Guid projectId, string bpmnXml, DateTime updatedAt)
		{
			var diagram = new Diagram { ProjectId = projectId };
			Apply(diagram, bpmnXml, updatedAt);
			return diagram;
		}

		public void Apply(Diagram diagram, string bpmnXml, DateTime updatedAt)
		{
			var contentSha256 = ContentHash.Sha256Hex(bpmnXml);
			var analysis = diagramAnalyzer.Analyze(contentSha256, bpmnXml);
			var limits = CalculatePlannedLimits(analysis, new ProjectContext(diagram.ProjectId, ProjectFactory.DefaultTimeUnit, ProjectFactory.DefaultCurrency));

			diagram.BpmnXml = bpmnXml;
			diagram.ContentSha256 = contentSha256;
			diagram.SizeBytes = Encoding.UTF8.GetByteCount(bpmnXml);
			diagram.FlowNodeCount = analysis.Graph.FlowNodeCount;
			diagram.ErrorCount = analysis.ErrorCount;
			diagram.WarningCount = analysis.WarningCount;
			diagram.IsOptimizable = analysis.IsOptimizable;
			diagram.MaxCost = limits?.MaxCost;
			diagram.MaxDurationBound = limits?.MaxDurationBound;
			diagram.UpdatedAt = updatedAt;
		}

		private ResourceLimitsDTO? CalculatePlannedLimits(DiagramAnalysis analysis, ProjectContext context) =>
			analysis.HasOptimizableStructure
				? resourceLimitCalculator.Calculate(analysis.Graph, analysis.Structure, EResourceLimitBasis.Planned, context)
				: null;
	}
}
