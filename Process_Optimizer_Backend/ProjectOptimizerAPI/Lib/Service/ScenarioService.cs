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
		public async Task<ScenarioCountDTO> GetScenarioCountAsync(Guid projectId, int? revision)
		{
			var indexed = await scenarioCatalog.GetIndexedRevisionAsync(projectId, revision);
			return new ScenarioCountDTO
			{
				RevisionNumber = indexed.Revision.Context.RevisionNumber,
				OrderingVersion = AnalysisLimits.OrderingVersion,
				Count = indexed.Count
			};
		}

		public async Task<PagedResultDTO<ScenarioListItemDTO>> ListScenariosAsync(Guid projectId, ScenarioFilterDTO filter)
		{
			var indexed = await scenarioCatalog.GetIndexedRevisionAsync(projectId, filter.Revision);
			var paging = PageRequest.Resolve(filter.Page, filter.PageSize);
			var pageIndices = SelectPageIndices(indexed, filter, paging);

			return new PagedResultDTO<ScenarioListItemDTO>
			{
				Items = pageIndices.Select(index => scenarioBuilder.ToListItem(diagramAnalyzer.GetScenario(indexed.Analysis, index))).ToList(),
				Page = paging.Page,
				PageSize = paging.PageSize,
				TotalCount = indexed.Count
			};
		}

		public async Task<ScenarioDTO> GetScenarioAsync(Guid projectId, long index, int? revision)
		{
			var indexed = await scenarioCatalog.GetIndexedRevisionAsync(projectId, revision);
			EnsureScenarioExists(indexed, index);
			var computation = diagramAnalyzer.GetScenario(indexed.Analysis, index);
			return scenarioBuilder.ToDto(computation, indexed.Revision.Context, indexed.Count);
		}

		public async Task<ResourceLimitsDTO> GetResourceLimitsAsync(Guid projectId, ResourceLimitsQueryDTO query)
		{
			var analyzed = await scenarioCatalog.GetOptimizableRevisionAsync(projectId, query.Revision);
			var analysis = analyzed.Analysis;
			var basis = query.Basis ?? EResourceLimitBasis.Planned;
			return resourceLimitCalculator.Calculate(analysis.Graph, analysis.Structure!, basis, analyzed.Revision.Context);
		}

		private IEnumerable<long> SelectPageIndices(IndexedRevision indexed, ScenarioFilterDTO filter, PageRequest paging)
		{
			var (offset, pageSize) = (paging.Offset, paging.PageSize);
			var sortBy = filter.SortBy ?? EScenarioSortBy.Index;
			var direction = filter.SortDirection ?? ESortDirection.Asc;
			EnsureScenariosCanBeSorted(sortBy, indexed.Count);
			if (offset >= indexed.Count)
			{
				return [];
			}

			return sortBy == EScenarioSortBy.Index
				? SelectCanonicalIndices(indexed.Count, direction, offset, pageSize)
				: diagramAnalyzer.GetSortedScenarioIndices(indexed.Analysis, sortBy, direction).Skip((int)offset).Take(pageSize);
		}

		private static IEnumerable<long> SelectCanonicalIndices(long count, ESortDirection direction, long offset, int pageSize)
		{
			var size = Math.Min(pageSize, count - offset);
			for (long position = offset; position < offset + size; position++)
			{
				yield return direction == ESortDirection.Asc ? position : count - 1 - position;
			}
		}

		private static void EnsureScenarioExists(IndexedRevision indexed, long index)
		{
			if (!indexed.Contains(index))
			{
				throw new NotFoundException(ErrorMessages.ScenarioNotFound);
			}
		}

		private static void EnsureScenariosCanBeSorted(EScenarioSortBy sortBy, long count)
		{
			if (sortBy != EScenarioSortBy.Index && !AnalysisLimits.IsSortable(count))
			{
				throw new InvalidRequestException(ErrorMessages.TooManyScenariosToSort);
			}
		}
	}
}
