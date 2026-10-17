using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class DiagramAnalyzerTests
	{
		private const string MalformedXml = "<bpmn:definitions";
		private const int ThousandNodeChainTasks = 998;
		private static readonly TimeSpan GenerousAnalysisLimit = TimeSpan.FromSeconds(5);

		private readonly AnalysisTestComponents _components = AnalysisTestComponents.Create();

		[Fact]
		public void Analyze_SameHashTwice_ReturnsCachedInstance()
		{
			var xml = BpmnFixtures.WorkedExample;
			var first = _components.Analyze(xml);

			var second = _components.Analyze(xml);

			Assert.Same(first, second);
		}

		[Fact]
		public void Analyze_WorkedExample_StoresLevelOneCacheEntry()
		{
			var xml = BpmnFixtures.WorkedExample;
			var sha = ContentHash.Sha256Hex(xml);

			var analysis = _components.Analyzer.Analyze(sha, xml);

			Assert.True(_components.Cache.TryGetValue($"scn-index:{sha}", out DiagramAnalysis? cached));
			Assert.Same(analysis, cached);
		}

		[Fact]
		public void Analyze_SameHashDifferentXml_ReturnsCachedAnalysisOfFirstContent()
		{
			var sha = ContentHash.Sha256Hex(BpmnFixtures.WorkedExample);
			var first = _components.Analyzer.Analyze(sha, BpmnFixtures.WorkedExample);

			var second = _components.Analyzer.Analyze(sha, DefaultDiagram.Xml);

			Assert.Same(first, second);
		}

		[Fact]
		public void Analyze_WorkedExample_IsOptimizableWithTwoScenarios()
		{
			var xml = BpmnFixtures.WorkedExample;

			var analysis = _components.Analyze(xml);

			Assert.True(analysis.IsOptimizable);
			Assert.NotNull(analysis.Structure);
			Assert.Equal(2, (long)analysis.ScenarioCount);
			Assert.Equal(0, analysis.ErrorCount);
			Assert.Equal(12, analysis.Graph.FlowNodeCount);
			Assert.Equal(ContentHash.Sha256Hex(xml), analysis.ContentSha256);
		}

		[Fact]
		public void Analyze_DefaultDiagram_HasNoStructure()
		{
			var xml = DefaultDiagram.Xml;

			var analysis = _components.Analyze(xml);

			Assert.False(analysis.IsOptimizable);
			Assert.Null(analysis.Structure);
			Assert.Equal(0, (long)analysis.ScenarioCount);
			Assert.True(analysis.ErrorCount > 0);
		}

		[Fact]
		public void Analyze_MalformedXml_ThrowsInvalidRequest()
		{
			var sha = ContentHash.Sha256Hex(MalformedXml);

			Assert.Throws<InvalidRequestException>(() => _components.Analyzer.Analyze(sha, MalformedXml));

			Assert.False(_components.Cache.TryGetValue($"scn-index:{sha}", out _));
		}

		[Fact]
		public void Analyze_ThousandNodeDiagram_CompletesWithinGenerousLimit()
		{
			var xml = BpmnDiagramBuilder.Chain(ThousandNodeChainTasks);
			var stopwatch = Stopwatch.StartNew();

			var analysis = _components.Analyze(xml);

			stopwatch.Stop();
			Assert.Equal(AnalysisLimits.MaxFlowNodes, analysis.Graph.FlowNodeCount);
			Assert.True(analysis.IsOptimizable);
			Assert.True(stopwatch.Elapsed < GenerousAnalysisLimit);
		}

		[Fact]
		public void GetScenario_SameIndexTwice_ReturnsCachedComputation()
		{
			var analysis = _components.Analyze(BpmnFixtures.WorkedExample);
			var first = _components.Analyzer.GetScenario(analysis, 1);

			var second = _components.Analyzer.GetScenario(analysis, 1);

			Assert.Same(first, second);
			Assert.True(_components.Cache.TryGetValue($"scn:{analysis.ContentSha256}:{AnalysisLimits.OrderingVersion}:1", out ScenarioComputation? cached));
			Assert.Same(first, cached);
		}

		[Fact]
		public void GetScenario_WorkedExampleFirstIndex_ReturnsOnPremScenario()
		{
			var analysis = _components.Analyze(BpmnFixtures.WorkedExample);

			var scenario = _components.Analyzer.GetScenario(analysis, 0);

			Assert.Equal(0, scenario.Selection.Index);
			Assert.Equal(23000m, scenario.TotalCost);
			Assert.Equal(21m, scenario.TotalDuration);
			Assert.Contains("E", scenario.ElementIds);
			Assert.DoesNotContain("F", scenario.ElementIds);
		}

		[Fact]
		public void GetScenario_DifferentIndices_ReturnDistinctScenarios()
		{
			var analysis = _components.Analyze(BpmnFixtures.WorkedExample);

			var first = _components.Analyzer.GetScenario(analysis, 0);
			var second = _components.Analyzer.GetScenario(analysis, 1);

			Assert.NotEqual(first.Selection.Key, second.Selection.Key);
			Assert.Equal(25000m, second.TotalCost);
			Assert.Equal(20m, second.TotalDuration);
		}

		[Theory]
		[InlineData(-1L)]
		[InlineData(2L)]
		public void GetScenario_IndexOutsideRange_ThrowsInvalidOperation(long index)
		{
			var analysis = _components.Analyze(BpmnFixtures.WorkedExample);

			Assert.Throws<InvalidOperationException>(() => _components.Analyzer.GetScenario(analysis, index));

			Assert.False(_components.Cache.TryGetValue($"scn:{analysis.ContentSha256}:{AnalysisLimits.OrderingVersion}:{index}", out _));
		}

		[Fact]
		public void GetScenario_DiagramWithoutStructure_ThrowsInvalidOperation()
		{
			var analysis = _components.Analyze(DefaultDiagram.Xml);

			Assert.Throws<InvalidOperationException>(() => _components.Analyzer.GetScenario(analysis, 0));
		}

		private static string TiedBranchesDiagram()
		{
			var builder = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Gateway("Split", EFlowNodeKind.ExclusiveGateway)
				.Gateway("Join", EFlowNodeKind.ExclusiveGateway)
				.EndEvent("End")
				.Flow("FlowStart", "Start", "Split")
				.Flow("FlowEnd", "Join", "End");
			foreach (var branch in new[] { "A", "B", "C" })
			{
				builder.Task($"Task{branch}", $"Task {branch}", 3, 500)
					.Flow($"FlowSplit{branch}", "Split", $"Task{branch}")
					.Flow($"FlowJoin{branch}", $"Task{branch}", "Join");
			}

			return builder.Build();
		}
	}
}
