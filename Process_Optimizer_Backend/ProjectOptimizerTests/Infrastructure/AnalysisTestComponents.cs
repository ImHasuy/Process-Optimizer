using Microsoft.Extensions.Caching.Memory;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;

namespace ProjectOptimizerTests.Infrastructure
{
	public sealed class AnalysisTestComponents
	{
		private const long CacheSizeLimit = 200_000;

		private AnalysisTestComponents()
		{
			Parser = new BpmnGraphParser();
			StructureAnalyzer = new ProcessStructureAnalyzer();
			Validator = new DiagramValidator(StructureAnalyzer);
			Indexer = new ScenarioIndexer();
			CriticalPathCalculator = new CriticalPathCalculator();
			ScenarioBuilder = new ScenarioBuilder(CriticalPathCalculator);
			ResourceLimitCalculator = new ResourceLimitCalculator();
			Cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = CacheSizeLimit });
			Analyzer = new DiagramAnalyzer(Parser, Validator, Indexer, ScenarioBuilder, Cache);
			DiagramFactory = new DiagramFactory(Analyzer, ResourceLimitCalculator);
		}

		public IBpmnGraphParser Parser { get; }
		public IProcessStructureAnalyzer StructureAnalyzer { get; }
		public IDiagramValidator Validator { get; }
		public IScenarioIndexer Indexer { get; }
		public ICriticalPathCalculator CriticalPathCalculator { get; }
		public IScenarioBuilder ScenarioBuilder { get; }
		public IResourceLimitCalculator ResourceLimitCalculator { get; }
		public IMemoryCache Cache { get; }
		public IDiagramAnalyzer Analyzer { get; }
		public IDiagramFactory DiagramFactory { get; }

		public static AnalysisTestComponents Create() => new();

		public DiagramAnalysis Analyze(string bpmnXml) => Analyzer.Analyze(ContentHash.Sha256Hex(bpmnXml), bpmnXml);
	}
}
