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
	public sealed class DiagramRevisionFactoryTests
	{
		private const string MalformedXml = "<bpmn:definitions";
		private const int RevisionNumber = 3;
		private static readonly Guid ProjectId = Guid.Parse("8a6e1c55-6f43-4d0b-9a8d-3f1f0c2b7e11");
		private static readonly DateTime CreatedAt = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

		private readonly AnalysisTestComponents _components = AnalysisTestComponents.Create();

		[Fact]
		public void Create_WorkedExample_StoresSummaryAndLimits()
		{
			var xml = BpmnFixtures.WorkedExample;

			var revision = Create(xml);

			Assert.NotEqual(Guid.Empty, revision.Id);
			Assert.Equal(ProjectId, revision.ProjectId);
			Assert.Equal(RevisionNumber, revision.RevisionNumber);
			Assert.Equal(CreatedAt, revision.CreatedAt);
			Assert.Equal(xml, revision.BpmnXml);
			Assert.Equal(12, revision.FlowNodeCount);
			Assert.Equal(0, revision.ErrorCount);
			Assert.True(revision.IsOptimizable);
			Assert.Equal(25000m, revision.MaxCost);
			Assert.Equal(25m, revision.MaxDurationBound);
		}

		[Fact]
		public void Create_SameXmlTwice_AssignsDistinctIds()
		{
			var xml = BpmnFixtures.WorkedExample;
			var first = Create(xml);

			var second = Create(xml);

			Assert.NotEqual(first.Id, second.Id);
			Assert.Equal(first.ContentSha256, second.ContentSha256);
		}

		[Fact]
		public void Create_DefaultDiagram_IsNotOptimizableWithNullLimits()
		{
			var xml = DefaultDiagram.Xml;

			var revision = Create(xml);

			Assert.False(revision.IsOptimizable);
			Assert.Null(revision.MaxCost);
			Assert.Null(revision.MaxDurationBound);
			Assert.Equal(1, revision.FlowNodeCount);
			Assert.True(revision.ErrorCount > 0);
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

			var revision = Create(xml);

			Assert.Equal(Encoding.UTF8.GetByteCount(xml), revision.SizeBytes);
			Assert.True(revision.SizeBytes > xml.Length);
			Assert.Equal(ContentHash.Sha256Hex(xml), revision.ContentSha256);
			Assert.Equal(64, revision.ContentSha256.Length);
			Assert.Matches("^[0-9a-f]{64}$", revision.ContentSha256);
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

			var revision = Create(xml);

			Assert.True(revision.ErrorCount > 0);
			Assert.False(revision.IsOptimizable);
			Assert.Null(revision.MaxCost);
			Assert.Null(revision.MaxDurationBound);
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

			var revision = Create(xml);

			Assert.True(revision.WarningCount > 0);
			Assert.Equal(0, revision.ErrorCount);
			Assert.True(revision.IsOptimizable);
			Assert.Equal(0m, revision.MaxCost);
		}

		[Fact]
		public void Create_WorkedExample_FillsLevelOneCache()
		{
			var xml = BpmnFixtures.WorkedExample;

			var revision = Create(xml);

			Assert.True(_components.Cache.TryGetValue($"scn-index:{revision.ContentSha256}", out DiagramAnalysis? cached));
			Assert.NotNull(cached);
		}

		[Fact]
		public void Create_MalformedXml_ThrowsInvalidRequest()
		{
			var xml = MalformedXml;

			Assert.Throws<InvalidRequestException>(() => Create(xml));
		}

		private DiagramRevision Create(string xml) =>
			_components.RevisionFactory.Create(ProjectId, RevisionNumber, xml, CreatedAt);
	}
}
