using ProjectOptimizerLib.Model.Process;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class ProcessDiagramTests
	{
		[Fact]
		public void Parse_FrontendExport_ReadsStatesAndBranches()
		{
			var diagram = ProcessDiagram.Parse(ProcessJsonFixtures.FrontendReadmeExportJson);

			Assert.Equal(3, diagram.States.Count);
			Assert.Equal(2, diagram.Branches.Count);
			var task = diagram.States[1];
			Assert.Equal("Activity_1", task.Id);
			Assert.Equal("Sample Task", task.Name);
			Assert.Equal("bpmn:Task", task.Type);
			Assert.Equal("15m", task.Time);
			Assert.Equal("25 EUR", task.Price);
			Assert.Equal(new[] { "StartEvent_1" }, task.DependsOn);
			Assert.Equal(new[] { "EndEvent_1" }, task.ConnectsTo);
			Assert.Equal("StartEvent_1", diagram.Branches[0].Source);
			Assert.Equal("Activity_1", diagram.Branches[0].Target);
			Assert.Equal(ProcessBranchTypes.Sequence, diagram.Branches[0].Type);
			Assert.Null(diagram.Branches[0].Condition);
		}

		[Fact]
		public void ToJson_RoundTrips_WithFrontendPropertyNames()
		{
			var json = ProcessDiagram.Parse(ProcessJsonFixtures.FrontendReadmeExportJson).ToJson();

			Assert.Contains("\"Depends_on\":[\"StartEvent_1\"]", json);
			Assert.Contains("\"Connects_to\":[\"EndEvent_1\"]", json);
			Assert.DoesNotContain("isDefault", json, StringComparison.OrdinalIgnoreCase);
			Assert.Equal(3, ProcessDiagram.Parse(json).States.Count);
		}

		[Fact]
		public void Parse_NullStatesAndBranches_GivesEmptyLists()
		{
			var diagram = ProcessDiagram.Parse("""{ "states": null, "branches": null }""");

			Assert.Empty(diagram.States);
			Assert.Empty(diagram.Branches);
		}

		[Theory]
		[InlineData("nor", true)]
		[InlineData(" NOR ", true)]
		[InlineData("xor", false)]
		[InlineData(null, false)]
		public void IsDefault_IsTrueOnlyForNorBranches(string? type, bool expected)
		{
			Assert.Equal(expected, new ProcessBranch { Type = type }.IsDefault);
		}
	}
}
