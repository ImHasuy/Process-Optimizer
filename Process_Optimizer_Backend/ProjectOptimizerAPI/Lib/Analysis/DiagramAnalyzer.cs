using Microsoft.Extensions.Caching.Memory;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Interface;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed class DiagramAnalyzer(IBpmnGraphParser bpmnGraphParser, IDiagramValidator diagramValidator, IScenarioIndexer scenarioIndexer, IScenarioBuilder scenarioBuilder, IMemoryCache memoryCache) : IDiagramAnalyzer
	{
		private const string NotIndexableMessage = "The diagram has no scenario structure.";
		private const string IndexOutOfRangeMessage = "The scenario index is outside the scenario range.";
		private static readonly TimeSpan AnalysisSlidingExpiration = TimeSpan.FromMinutes(30);
		private static readonly TimeSpan ScenarioSlidingExpiration = TimeSpan.FromMinutes(10);

		public DiagramAnalysis Analyze(string contentSha256, string bpmnXml) =>
			GetOrCompute(
				$"scn-index:{contentSha256}",
				() => CreateAnalysis(contentSha256, bpmnXml),
				analysis => analysis.Graph.Nodes.Count,
				AnalysisSlidingExpiration);

		public ScenarioComputation GetScenario(DiagramAnalysis analysis, long index)
		{
			var structure = EnsureIndexable(analysis);
			EnsureIndexInRange(analysis, index);
			return GetOrCompute(
				$"scn:{analysis.ContentSha256}:{AnalysisLimits.OrderingVersion}:{index}",
				() => ComputeScenario(analysis.Graph, structure, index),
				computation => computation.Activities.Count,
				ScenarioSlidingExpiration);
		}

		private DiagramAnalysis CreateAnalysis(string contentSha256, string bpmnXml)
		{
			var graph = bpmnGraphParser.Parse(bpmnXml);
			var validation = diagramValidator.Validate(graph);
			return new DiagramAnalysis(contentSha256, graph, validation.Issues, validation.IsOptimizable, validation.Structure);
		}

		private ScenarioComputation ComputeScenario(ProcessGraph graph, StructureFragment structure, long index) =>
			scenarioBuilder.Build(graph, scenarioIndexer.Unrank(structure, index));

		private TValue GetOrCompute<TValue>(string cacheKey, Func<TValue> compute, Func<TValue, long> size, TimeSpan slidingExpiration)
			where TValue : class
		{
			if (memoryCache.TryGetValue(cacheKey, out TValue? cached) && cached is not null)
			{
				return cached;
			}

			var value = compute();
			var options = new MemoryCacheEntryOptions()
				.SetSize(Math.Max(1, size(value)))
				.SetSlidingExpiration(slidingExpiration);
			return memoryCache.Set(cacheKey, value, options);
		}

		private static StructureFragment EnsureIndexable(DiagramAnalysis analysis) =>
			analysis.Structure ?? throw new InvalidOperationException(NotIndexableMessage);

		private static void EnsureIndexInRange(DiagramAnalysis analysis, long index)
		{
			if (!AnalysisLimits.IsIndexInRange(index, analysis.ScenarioCount))
			{
				throw new InvalidOperationException(IndexOutOfRangeMessage);
			}
		}
	}
}
