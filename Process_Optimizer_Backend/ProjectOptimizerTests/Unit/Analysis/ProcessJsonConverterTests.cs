using System.Xml.Linq;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerLib.Model.Process;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;
using static ProjectOptimizerTests.Infrastructure.ProcessJsonFixtures;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class ProcessJsonConverterTests
	{
		private static readonly XNamespace ModelNamespace = BpmnNamespaces.Model;
		private static readonly XNamespace DiNamespace = BpmnNamespaces.Di;
		private static readonly XNamespace DiagramCommonNamespace = BpmnNamespaces.DiagramCommon;
		private static readonly XNamespace FrontendNamespace = BpmnNamespaces.Frontend;

		private readonly ProcessJsonConverter _converter = new();
		private readonly AnalysisTestComponents _analysis = AnalysisTestComponents.Create();

		[Fact]
		public void ToBpmnXml_SampleExport_ProducesOptimizableDiagramWithoutIssues()
		{
			var xml = Convert(SampleStates(), SampleBranches());

			var analysis = _analysis.Analyze(xml);

			Assert.True(analysis.IsOptimizable);
			Assert.Equal(0, analysis.ErrorCount);
			Assert.Equal(0, analysis.WarningCount);
			Assert.Equal(3, analysis.Graph.Nodes.Count);
			Assert.Equal(2, analysis.Graph.Edges.Count);
			Assert.Equal("Sample Task", analysis.Graph.NodesById["Activity_1"].Name);
			Assert.Equal(new[] { "StartEvent_1" }, analysis.Graph.NodesById["Activity_1"].DependsOn);
		}

		[Theory]
		[InlineData(ETimeUnit.Minutes, "15")]
		[InlineData(ETimeUnit.Hours, "0.25")]
		[InlineData(ETimeUnit.Days, "0.010417")]
		public void ToBpmnXml_TimeWithUnit_StoresDurationInProjectUnit(ETimeUnit timeUnit, string expected)
		{
			var xml = _converter.ToBpmnXml(Diagram(SampleStates(), SampleBranches()), timeUnit, "EUR");

			var graph = _analysis.Parser.Parse(xml);

			Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), graph.NodesById["Activity_1"].Duration);
			Assert.Equal(25m, graph.NodesById["Activity_1"].Cost);
		}

		[Fact]
		public void ToBpmnXml_TimeAndPrice_KeepsOriginalTextInFrontendNamespace()
		{
			var task = TaskElement(Convert(SampleStates(), SampleBranches()), "Activity_1");

			Assert.Equal("15m", (string?)task.Attribute(FrontendNamespace + "time"));
			Assert.Equal("25 EUR", (string?)task.Attribute(FrontendNamespace + "price"));
		}

		[Fact]
		public void ToBpmnXml_EmptyTimeAndPrice_WritesNoMetricAttributes()
		{
			var start = TaskElement(Convert(SampleStates(), SampleBranches()), "StartEvent_1");

			Assert.DoesNotContain(start.Attributes(), attribute => attribute.Name.Namespace != XNamespace.None);
		}

		[Fact]
		public void ToBpmnXml_PriceInOtherCurrency_IsKeptAndReportedAsInvalidValue()
		{
			var states = SampleStates();
			states[1].Price = "$40";

			var analysis = _analysis.Analyze(Convert(states, SampleBranches()));

			var issue = Assert.Single(analysis.Issues, issue => issue.Code == DiagramIssueCodes.InvalidValue);
			Assert.Equal("Activity_1", issue.BpmnElementId);
			Assert.Contains("'$40'", issue.Message);
		}

		[Fact]
		public void ToBpmnXml_ExclusiveChoice_KeepsDefaultFlowAndConditionAndIsOptimizable()
		{
			var analysis = _analysis.Analyze(Convert(ExclusiveChoiceStates(), ExclusiveChoiceBranches()));

			Assert.True(analysis.IsOptimizable);
			Assert.True(analysis.Graph.Edges.Single(edge => edge.BpmnId == "F2").IsDefault);
			Assert.False(analysis.Graph.Edges.Single(edge => edge.BpmnId == "F3").IsDefault);
			Assert.Equal("budget > 1000", analysis.Graph.Edges.Single(edge => edge.BpmnId == "F3").ConditionExpression);
			Assert.Equal(EFlowNodeKind.ExclusiveGateway, analysis.Graph.NodesById["Split"].Kind);
			Assert.Equal(2m, analysis.Graph.NodesById["Cheap"].Duration);
		}

		[Fact]
		public void ToBpmnXml_EveryNodeAndBranch_GetsDiagramElements()
		{
			var document = XDocument.Parse(Convert(ExclusiveChoiceStates(), ExclusiveChoiceBranches()));

			var shapes = document.Descendants(DiNamespace + "BPMNShape").Select(shape => (string?)shape.Attribute("bpmnElement")).ToList();
			var edges = document.Descendants(DiNamespace + "BPMNEdge").Select(edge => (string?)edge.Attribute("bpmnElement")).ToList();
			Assert.Equal(ExclusiveChoiceStates().Select(state => state.Id), shapes);
			Assert.Equal(ExclusiveChoiceBranches().Select(branch => branch.Id), edges);
			Assert.All(document.Descendants(DiNamespace + "BPMNEdge"), edge => Assert.True(edge.Elements().Count() >= 2));
		}

		[Fact]
		public void ToBpmnXml_Layout_PlacesSuccessorsToTheRightAndParallelBranchesOnSeparateRows()
		{
			var document = XDocument.Parse(Convert(ExclusiveChoiceStates(), ExclusiveChoiceBranches()));

			Assert.True(CenterX(document, "Split") > CenterX(document, "Start"));
			Assert.True(CenterX(document, "Cheap") > CenterX(document, "Split"));
			Assert.Equal(CenterX(document, "Cheap"), CenterX(document, "Fast"));
			Assert.NotEqual((int)Bounds(document, "Cheap").Attribute("y")!, (int)Bounds(document, "Fast").Attribute("y")!);
			Assert.True(CenterX(document, "End") > CenterX(document, "Join"));
			Assert.Equal(36, (int)Bounds(document, "Start").Attribute("width")!);
			Assert.Equal(50, (int)Bounds(document, "Split").Attribute("width")!);
			Assert.Equal(100, (int)Bounds(document, "Cheap").Attribute("width")!);
		}

		[Fact]
		public void ToBpmnXml_CyclicBranches_StillLaysOutEveryNodeAndReportsCycle()
		{
			var states = new List<ProcessState> { State("S", "bpmn:StartEvent"), State("A", "bpmn:Task", "1", "1"), State("B", "bpmn:Task", "1", "1"), State("E", "bpmn:EndEvent") };
			var branches = new List<ProcessBranch> { Branch("F1", "S", "A"), Branch("F2", "A", "B"), Branch("F3", "B", "A"), Branch("F4", "B", "E") };

			var xml = Convert(states, branches);

			Assert.Equal(4, XDocument.Parse(xml).Descendants(DiNamespace + "BPMNShape").Count());
			Assert.Contains(_analysis.Analyze(xml).Issues, issue => issue.Code == DiagramIssueCodes.Cycle);
		}

		[Fact]
		public void ToBpmnXml_ArtifactStates_AreLeftOut()
		{
			var states = SampleStates();
			states.Add(State("TextAnnotation_1", "bpmn:TextAnnotation"));
			states.Add(State("Association_1", "bpmn:Association"));

			var document = XDocument.Parse(Convert(states, SampleBranches()));

			Assert.Equal(3, document.Descendants(ModelNamespace + "process").Single().Elements().Count(element => element.Name.LocalName != "sequenceFlow"));
			Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute("id") == "TextAnnotation_1");
		}

		[Fact]
		public void ToBpmnXml_UnsupportedType_IsKeptAndReportedByValidation()
		{
			var states = SampleStates();
			states.Add(State("Complex_1", "bpmn:ComplexGateway"));

			var analysis = _analysis.Analyze(Convert(states, SampleBranches()));

			Assert.Contains(analysis.Issues, issue => issue.Code == DiagramIssueCodes.UnsupportedElement && issue.BpmnElementId == "Complex_1");
		}

		[Fact]
		public void ToBpmnXml_BranchToUnknownState_IsReportedAsDanglingWithoutDiagramEdge()
		{
			var branches = SampleBranches();
			branches.Add(Branch("Flow_X", "Activity_1", "Missing"));

			var xml = Convert(SampleStates(), branches);

			Assert.DoesNotContain(XDocument.Parse(xml).Descendants(DiNamespace + "BPMNEdge"), edge => (string?)edge.Attribute("bpmnElement") == "Flow_X");
			Assert.Contains(_analysis.Analyze(xml).Issues, issue => issue.Code == DiagramIssueCodes.DanglingSequenceFlow && issue.BpmnElementId == "Flow_X");
		}

		[Fact]
		public void ToBpmnXml_StateNamedLikeGeneratedId_GetsUniqueGeneratedIds()
		{
			var states = SampleStates();
			states[1].Id = "Process_1";
			var branches = new List<ProcessBranch> { Branch("Flow_1", "StartEvent_1", "Process_1"), Branch("Flow_2", "Process_1", "EndEvent_1") };

			var document = XDocument.Parse(Convert(states, branches));

			var ids = document.Descendants().Select(element => (string?)element.Attribute("id")).OfType<string>().ToList();
			Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
			Assert.Equal("Process_1_2", (string?)document.Descendants(ModelNamespace + "process").Single().Attribute("id"));
		}

		[Theory]
		[InlineData("1abc")]
		[InlineData("has space")]
		[InlineData("a:b")]
		public void ToBpmnXml_InvalidId_ThrowsInvalidRequest(string id)
		{
			var states = SampleStates();
			states[1].Id = id;

			var exception = Assert.Throws<InvalidRequestException>(() => Convert(states, SampleBranches()));

			Assert.Equal($"The id '{id}' is not a valid BPMN identifier.", exception.Message);
		}

		[Fact]
		public void ToBpmnXml_IdUsedByStateAndBranch_ThrowsInvalidRequest()
		{
			var branches = SampleBranches();
			branches[0].Id = "Activity_1";

			var exception = Assert.Throws<InvalidRequestException>(() => Convert(SampleStates(), branches));

			Assert.Equal("The id 'Activity_1' is used more than once.", exception.Message);
		}

		[Theory]
		[InlineData("Task")]
		[InlineData("bpmn:task")]
		[InlineData("label")]
		[InlineData("bpmn:")]
		public void ToBpmnXml_TypeWithoutBpmnPrefix_ThrowsInvalidRequest(string type)
		{
			var states = SampleStates();
			states[1].Type = type;

			var exception = Assert.Throws<InvalidRequestException>(() => Convert(states, SampleBranches()));

			Assert.Equal($"The type '{type}' of state 'Activity_1' is not a BPMN element type such as 'bpmn:Task'.", exception.Message);
		}

		[Fact]
		public void ToBpmnXml_TwoDefaultBranchesFromOneState_ThrowsInvalidRequest()
		{
			var branches = ExclusiveChoiceBranches();
			branches[2].Type = "NOR";

			var exception = Assert.Throws<InvalidRequestException>(() => Convert(ExclusiveChoiceStates(), branches));

			Assert.Equal("The state 'Split' has more than one 'nor' (default) branch.", exception.Message);
		}

		private string Convert(List<ProcessState> states, List<ProcessBranch> branches) =>
			_converter.ToBpmnXml(Diagram(states, branches), ETimeUnit.Days, "EUR");

		private static XElement TaskElement(string xml, string id) =>
			XDocument.Parse(xml).Descendants(ModelNamespace + "process").Single().Elements().Single(element => (string?)element.Attribute("id") == id);

		private static XElement Bounds(XDocument document, string id) =>
			document.Descendants(DiNamespace + "BPMNShape").Single(shape => (string?)shape.Attribute("bpmnElement") == id).Element(DiagramCommonNamespace + "Bounds")!;

		private static int CenterX(XDocument document, string id)
		{
			var bounds = Bounds(document, id);
			return (int)bounds.Attribute("x")! + (int)bounds.Attribute("width")! / 2;
		}
	}
}
