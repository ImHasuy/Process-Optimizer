using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Service
{
	public sealed class ScenarioService(IScenarioCatalog scenarioCatalog, IDiagramAnalyzer diagramAnalyzer, IScenarioBuilder scenarioBuilder, IResourceLimitCalculator resourceLimitCalculator) : IScenarioService
	{
		public async Task<ScenarioCountDTO> GetScenarioCountAsync(Guid projectId)
		{
			var indexed = await scenarioCatalog.GetIndexedDiagramAsync(projectId);
			return new ScenarioCountDTO { OrderingVersion = AnalysisLimits.OrderingVersion, Count = indexed.Count };
		}

		public async Task<PagedResultDTO<ScenarioListItemDTO>> ListScenariosAsync(Guid projectId, ScenarioFilterDTO filter)
		{
			var indexed = await scenarioCatalog.GetIndexedDiagramAsync(projectId);
			var paging = PageRequest.Resolve(filter.Page, filter.PageSize);

			return new PagedResultDTO<ScenarioListItemDTO>
			{
				Items = SelectPageIndices(indexed.Count, paging).Select(index => scenarioBuilder.ToListItem(diagramAnalyzer.GetScenario(indexed.Analysis, index))).ToList(),
				Page = paging.Page,
				PageSize = paging.PageSize,
				TotalCount = indexed.Count
			};
		}

		public async Task<ScenarioDTO> GetScenarioAsync(Guid projectId, long index)
		{
			var indexed = await scenarioCatalog.GetIndexedDiagramAsync(projectId);
			EnsureScenarioExists(indexed, index);
			var computation = diagramAnalyzer.GetScenario(indexed.Analysis, index);
			return scenarioBuilder.ToDto(computation, indexed.Diagram.Context, indexed.Count);
		}

		public async Task<ResourceLimitsDTO> GetResourceLimitsAsync(Guid projectId, ResourceLimitsQueryDTO query)
		{
			var analyzed = await scenarioCatalog.GetOptimizableDiagramAsync(projectId);
			var analysis = analyzed.Analysis;
			var basis = query.Basis ?? EResourceLimitBasis.Planned;
			return resourceLimitCalculator.Calculate(analysis.Graph, analysis.Structure!, basis, analyzed.Diagram.Context);
		}

		private static IEnumerable<long> SelectPageIndices(long count, PageRequest paging)
		{
			for (var index = paging.Offset; index < Math.Min(count, paging.Offset + paging.PageSize); index++)
			{
				yield return index;
			}
		}

		private static void EnsureScenarioExists(IndexedDiagram indexed, long index)
		{
			if (!indexed.Contains(index))
			{
				throw new NotFoundException(ErrorMessages.ScenarioNotFound);
			}
		}
	}
}
