using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class ScenarioCatalog(IRevisionResolver revisionResolver, IDiagramAnalyzer diagramAnalyzer, IScenarioIndexer scenarioIndexer) : IScenarioCatalog
	{
		public async Task<AnalyzedRevision> GetOptimizableRevisionAsync(Guid projectId, int? revisionNumber)
		{
			var analyzed = await AnalyzeRevisionAsync(projectId, revisionNumber);
			EnsureOptimizable(analyzed.Analysis);
			return analyzed;
		}

		public async Task<IndexedRevision> GetIndexedRevisionAsync(Guid projectId, int? revisionNumber)
		{
			var analyzed = await GetOptimizableRevisionAsync(projectId, revisionNumber);
			EnsureIndexable(analyzed.Analysis);
			return new IndexedRevision(analyzed.Revision, analyzed.Analysis, (long)analyzed.Analysis.ScenarioCount);
		}

		public async Task<Func<string, long?>> CreateLatestRevisionRankerAsync(Guid projectId)
		{
			var analysis = (await AnalyzeRevisionAsync(projectId, null)).Analysis;
			if (!IsIndexable(analysis))
			{
				return _ => null;
			}

			var structure = analysis.Structure!;
			return scenarioKey => scenarioIndexer.Rank(structure, scenarioKey);
		}

		private async Task<AnalyzedRevision> AnalyzeRevisionAsync(Guid projectId, int? revisionNumber)
		{
			var revision = await revisionResolver.ResolveAsync(projectId, revisionNumber);
			var analysis = diagramAnalyzer.Analyze(revision.ContentSha256, revision.BpmnXml);
			return new AnalyzedRevision(revision, analysis);
		}

		private static bool IsIndexable(DiagramAnalysis analysis) =>
			analysis.HasOptimizableStructure && analysis.IsIndexable;

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
