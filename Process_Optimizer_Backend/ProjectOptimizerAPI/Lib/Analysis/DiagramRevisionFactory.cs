using System.Text;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed class DiagramRevisionFactory(IDiagramAnalyzer diagramAnalyzer, IResourceLimitCalculator resourceLimitCalculator) : IDiagramRevisionFactory
	{
		public DiagramRevision Create(Guid projectId, int revisionNumber, string bpmnXml, DateTime createdAt)
		{
			var contentSha256 = ContentHash.Sha256Hex(bpmnXml);
			var analysis = diagramAnalyzer.Analyze(contentSha256, bpmnXml);
			var limits = CalculatePlannedLimits(analysis, new RevisionContext(projectId, revisionNumber, ProjectFactory.DefaultTimeUnit, ProjectFactory.DefaultCurrency));
			return new DiagramRevision
			{
				Id = Guid.NewGuid(),
				ProjectId = projectId,
				RevisionNumber = revisionNumber,
				BpmnXml = bpmnXml,
				ContentSha256 = contentSha256,
				SizeBytes = Encoding.UTF8.GetByteCount(bpmnXml),
				FlowNodeCount = analysis.Graph.FlowNodeCount,
				ErrorCount = analysis.ErrorCount,
				WarningCount = analysis.WarningCount,
				IsOptimizable = analysis.IsOptimizable,
				MaxCost = limits?.MaxCost,
				MaxDurationBound = limits?.MaxDurationBound,
				CreatedAt = createdAt
			};
		}

		private ResourceLimitsDTO? CalculatePlannedLimits(DiagramAnalysis analysis, RevisionContext context) =>
			analysis.HasOptimizableStructure
				? resourceLimitCalculator.Calculate(analysis.Graph, analysis.Structure, EResourceLimitBasis.Planned, context)
				: null;
	}
}
