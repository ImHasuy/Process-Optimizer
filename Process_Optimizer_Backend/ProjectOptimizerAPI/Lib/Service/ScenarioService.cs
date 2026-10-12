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

		public async Task<List<ScenarioDTO>> GetAllScenariosAsync(Guid projectId)
		{
			var indexed = await scenarioCatalog.GetIndexedDiagramAsync(projectId);
			if (indexed.Count > AnalysisLimits.MaxListedScenarios)
			{
				throw new ConflictException(ErrorMessages.TooManyScenariosToList);
			}

			var scenarios = new List<ScenarioDTO>();
			for (var index = 0L; index < indexed.Count; index++)
			{
				scenarios.Add(BuildScenario(indexed, index));
			}

			return scenarios;
		}

		public async Task<ScenarioDTO> GetScenarioAsync(Guid projectId, long index)
		{
			var indexed = await scenarioCatalog.GetIndexedDiagramAsync(projectId);
			EnsureScenarioExists(indexed, index);
			return BuildScenario(indexed, index);
		}

		public async Task<ResourceLimitsDTO> GetResourceLimitsAsync(Guid projectId, ResourceLimitsQueryDTO query)
		{
			var analyzed = await scenarioCatalog.GetOptimizableDiagramAsync(projectId);
			var analysis = analyzed.Analysis;
			var basis = query.Basis ?? EResourceLimitBasis.Planned;
			return resourceLimitCalculator.Calculate(analysis.Graph, analysis.Structure!, basis, analyzed.Diagram.Context);
		}

		private ScenarioDTO BuildScenario(IndexedDiagram indexed, long index) =>
			scenarioBuilder.ToDto(diagramAnalyzer.GetScenario(indexed.Analysis, index), indexed.Diagram.Context, indexed.Count);

		private static void EnsureScenarioExists(IndexedDiagram indexed, long index)
		{
			if (!indexed.Contains(index))
			{
				throw new NotFoundException(ErrorMessages.ScenarioNotFound);
			}
		}
	}
}
