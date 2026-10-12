using System.Text;
using Microsoft.Extensions.Caching.Memory;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.Model;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class DiagramFactoryTests
	{
		private const string MalformedXml = "<bpmn:definitions";
		private static readonly Guid ProjectId = Guid.Parse("8a6e1c55-6f43-4d0b-9a8d-3f1f0c2b7e11");
		private static readonly DateTime UpdatedAt = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
		private static readonly DateTime LaterUpdatedAt = new(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);

		private readonly AnalysisTestComponents _components = AnalysisTestComponents.Create();

		[Fact]
		public void Create_WorkedExample_StoresSummaryAndLimits()
		{
			var xml = BpmnFixtures.WorkedExample;

			var diagram = Create(xml);

			Assert.Equal(ProjectId, diagram.ProjectId);
			Assert.Equal(UpdatedAt, diagram.UpdatedAt);
			Assert.Equal(xml, diagram.BpmnXml);
			Assert.Equal(12, diagram.FlowNodeCount);
			Assert.Equal(0, diagram.ErrorCount);
			Assert.True(diagram.IsOptimizable);
			Assert.Equal(25000m, diagram.MaxCost);
			Assert.Equal(25m, diagram.MaxDurationBound);
		}

		[Fact]
		public void Apply_ExistingDiagram_ReplacesContentAndSummary()
		{
			var diagram = Create(DefaultDiagram.Xml);
			var xml = BpmnFixtures.WorkedExample;

			_components.DiagramFactory.Apply(diagram, xml, LaterUpdatedAt);

			Assert.Equal(ProjectId, diagram.ProjectId);
			Assert.Equal(LaterUpdatedAt, diagram.UpdatedAt);
			Assert.Equal(xml, diagram.BpmnXml);
			Assert.Equal(ContentHash.Sha256Hex(xml), diagram.ContentSha256);
			Assert.Equal(12, diagram.FlowNodeCount);
			Assert.Equal(0, diagram.ErrorCount);
			Assert.True(diagram.IsOptimizable);
			Assert.Equal(25000m, diagram.MaxCost);
			Assert.Equal(25m, diagram.MaxDurationBound);
		}

		[Fact]
		public void Apply_OptimizableToDefault_ClearsLimits()
		{
			var diagram = Create(BpmnFixtures.WorkedExample);

			_components.DiagramFactory.Apply(diagram, DefaultDiagram.Xml, LaterUpdatedAt);

			Assert.False(diagram.IsOptimizable);
			Assert.Null(diagram.MaxCost);
			Assert.Null(diagram.MaxDurationBound);
			Assert.True(diagram.ErrorCount > 0);
		}

		[Fact]
		public void Apply_MalformedXml_ThrowsAndLeavesDiagramUnchanged()
		{
			var diagram = Create(BpmnFixtures.WorkedExample);
			var originalSha256 = diagram.ContentSha256;

			Assert.Throws<InvalidRequestException>(() => _components.DiagramFactory.Apply(diagram, MalformedXml, LaterUpdatedAt));

			Assert.Equal(BpmnFixtures.WorkedExample, diagram.BpmnXml);
			Assert.Equal(originalSha256, diagram.ContentSha256);
			Assert.Equal(UpdatedAt, diagram.UpdatedAt);
			Assert.Equal(25000m, diagram.MaxCost);
		}

		[Fact]
		public void Create_DefaultDiagram_IsNotOptimizableWithNullLimits()
		{
			var xml = DefaultDiagram.Xml;

			var diagram = Create(xml);

			Assert.False(diagram.IsOptimizable);
			Assert.Null(diagram.MaxCost);
			Assert.Null(diagram.MaxDurationBound);
			Assert.Equal(1, diagram.FlowNodeCount);
			Assert.True(diagram.ErrorCount > 0);
		}

		[Fact]
		public void Create_ComputesUtf8SizeAndLowercaseSha256()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start", "Indulás")
				.Task("Task", "Tervezés és ütemezés", 2, 300)
				.EndEvent("End", "Kész")
				.Flow("Flow1", "Start", "Task")
				.Flow("Flow2", "Task", "End")
				.Build();

			var diagram = Create(xml);

			Assert.Equal(Encoding.UTF8.GetByteCount(xml), diagram.SizeBytes);
			Assert.True(diagram.SizeBytes > xml.Length);
			Assert.Equal(ContentHash.Sha256Hex(xml), diagram.ContentSha256);
			Assert.Equal(64, diagram.ContentSha256.Length);
			Assert.Matches("^[0-9a-f]{64}$", diagram.ContentSha256);
		}

		[Fact]
		public void Create_DiagramWithSemanticErrors_DoesNotThrow()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("Task", "Dead end", 1, 100)
				.Flow("Flow1", "Start", "Task")
				.Flow("Dangling", "Task", "Missing")
				.Build();

			var diagram = Create(xml);

			Assert.True(diagram.ErrorCount > 0);
			Assert.False(diagram.IsOptimizable);
			Assert.Null(diagram.MaxCost);
			Assert.Null(diagram.MaxDurationBound);
		}

		[Fact]
		public void Create_DiagramWithMissingEstimates_RecordsWarningsAndStaysOptimizable()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Task("Task", "Unestimated", null, null)
				.EndEvent("End")
				.Flow("Flow1", "Start", "Task")
				.Flow("Flow2", "Task", "End")
				.Build();

			var diagram = Create(xml);

			Assert.True(diagram.WarningCount > 0);
			Assert.Equal(0, diagram.ErrorCount);
			Assert.True(diagram.IsOptimizable);
			Assert.Equal(0m, diagram.MaxCost);
		}

		[Fact]
		public void Create_WorkedExample_FillsLevelOneCache()
		{
			var xml = BpmnFixtures.WorkedExample;

			var diagram = Create(xml);

			Assert.True(_components.Cache.TryGetValue($"scn-index:{diagram.ContentSha256}", out DiagramAnalysis? cached));
			Assert.NotNull(cached);
		}

		[Fact]
		public void Create_MalformedXml_ThrowsInvalidRequest()
		{
			var xml = MalformedXml;

			Assert.Throws<InvalidRequestException>(() => Create(xml));
		}

		private Diagram Create(string xml) =>
			_components.DiagramFactory.Create(ProjectId, xml, UpdatedAt);
	}
}
