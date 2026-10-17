using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class ScenarioCatalog(IDiagramResolver diagramResolver, IDiagramAnalyzer diagramAnalyzer) : IScenarioCatalog
	{
		public async Task<AnalyzedDiagram> GetOptimizableDiagramAsync(Guid projectId)
		{
			var analyzed = await AnalyzeAsync(projectId);
			EnsureOptimizable(analyzed.Analysis);
			return analyzed;
		}

		public async Task<IndexedDiagram> GetIndexedDiagramAsync(Guid projectId)
		{
			var analyzed = await GetOptimizableDiagramAsync(projectId);
			EnsureIndexable(analyzed.Analysis);
			return new IndexedDiagram(analyzed.Diagram, analyzed.Analysis, (long)analyzed.Analysis.ScenarioCount);
		}

		private async Task<AnalyzedDiagram> AnalyzeAsync(Guid projectId)
		{
			var diagram = await diagramResolver.ResolveAsync(projectId);
			var analysis = diagramAnalyzer.Analyze(diagram.ContentSha256, diagram.BpmnXml);
			return new AnalyzedDiagram(diagram, analysis);
		}

		private static void EnsureOptimizable(DiagramAnalysis analysis)
		{
			if (!analysis.HasOptimizableStructure)
			{
				throw new ConflictException(ErrorMessages.DiagramHasErrors);
			}
		}

		private static void EnsureIndexable(DiagramAnalysis analysis)
		{
			if (!analysis.IsIndexable)
			{
				throw new ConflictException(ErrorMessages.TooManyScenarios);
			}
		}
	}
}
