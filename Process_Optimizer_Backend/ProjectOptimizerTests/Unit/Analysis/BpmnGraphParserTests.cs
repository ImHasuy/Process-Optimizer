using System.Globalization;
using System.Text;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerLib.Enum;
using ProjectOptimizerTests.Infrastructure;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class BpmnGraphParserTests
	{
		private const string XmlTooLargeMessage = "The diagram XML must not be larger than 2 MB.";
		private const string NotWellFormedMessage = "The diagram is not well-formed XML or contains a DTD.";
		private const string InvalidRootMessage = "The diagram root element must be a BPMN 2.0 definitions element.";
		private const string TooManyFlowNodesMessage = "The diagram must not contain more than 1000 flow nodes.";
		private const string InvalidCrashOptionMessage = "A crash option must be faster than the normal duration and not cheaper than the normal cost.";
		private const string UnorderedEstimateMessage = "The estimate must satisfy optimistic <= mostLikely <= pessimistic.";

		private readonly BpmnGraphParser _parser = new();

		[Fact]
		public void Parse_WorkedExample_ReturnsTwelveNodesAndThirteenEdges()
		{
			var xml = BpmnFixtures.WorkedExample;

			var graph = _parser.Parse(xml);

			Assert.Equal("Process_1", graph.ProcessId);
			Assert.Equal(12, graph.Nodes.Count);
			Assert.Equal(13, graph.Edges.Count);
			Assert.Equal(12, graph.FlowNodeCount);
			Assert.Equal(12, graph.NodesById.Count);
			Assert.Empty(graph.ParseIssues);
		}

		[Fact]
		public void Parse_WorkedExample_DerivesDependsOnAndDependents()
		{
			var xml = BpmnFixtures.WorkedExample;

			var graph = _parser.Parse(xml);

			Assert.Equal(new[] { "B", "C" }, graph.NodesById["G1"].Dependents);
			Assert.Equal(new[] { "B", "C" }, graph.NodesById["G2"].DependsOn);
			Assert.Equal(new[] { "G2" }, graph.NodesById["D"].DependsOn);
			Assert.Equal(new[] { "G3" }, graph.NodesById["D"].Dependents);
			Assert.Empty(graph.NodesById["S"].DependsOn);
			Assert.Empty(graph.NodesById["End"].Dependents);
		}

		[Fact]
		public void Parse_WorkedExample_ClassifiesGatewayDirections()
		{
			var xml = BpmnFixtures.WorkedExample;

			var graph = _parser.Parse(xml);

			Assert.Equal(EGatewayDirection.Split, graph.NodesById["G1"].GatewayDirection);
			Assert.Equal(EGatewayDirection.Join, graph.NodesById["G2"].GatewayDirection);
			Assert.Equal(EGatewayDirection.Split, graph.NodesById["G3"].GatewayDirection);
			Assert.Equal(EGatewayDirection.Join, graph.NodesById["G4"].GatewayDirection);
			Assert.Equal(EGatewayDirection.None, graph.NodesById["A"].GatewayDirection);
		}

		[Fact]
		public void Parse_WorkedExample_ReadsDurationCostAndParallelizable()
		{
			var xml = BpmnFixtures.WorkedExample;

			var graph = _parser.Parse(xml);

			var test = graph.NodesById["D"];
			Assert.Equal(4m, test.Duration);
			Assert.Equal(2000m, test.Cost);
			Assert.True(test.HasDuration);
			Assert.True(test.HasCost);
			Assert.False(test.Parallelizable);
			Assert.True(graph.NodesById["A"].Parallelizable);
			Assert.Equal("Design", graph.NodesById["A"].Name);
			Assert.Equal(EFlowNodeKind.ParallelGateway, graph.NodesById["G1"].Kind);
			Assert.Equal(EFlowNodeKind.ExclusiveGateway, graph.NodesById["G3"].Kind);
		}

		[Fact]
		public void Parse_DefaultDiagram_ReturnsSingleStartEvent()
		{
			var xml = DefaultDiagram.Xml;

			var graph = _parser.Parse(xml);

			var node = Assert.Single(graph.Nodes);
			Assert.Equal("StartEvent_1", node.BpmnId);
			Assert.Equal(EFlowNodeKind.StartEvent, node.Kind);
			Assert.Empty(graph.Edges);
			Assert.Empty(graph.ParseIssues);
		}

		[Fact]
		public void Parse_Bpmn2Prefix_ProducesSameGraphAsBpmnPrefix()
		{
			var expected = Describe(_parser.Parse(BpmnFixtures.WorkedExample));

			var graph = _parser.Parse(BpmnFixtures.Load("Parsing/bpmn2-prefix.bpmn"));

			Assert.Equal(expected, Describe(graph));
		}

		[Fact]
		public void Parse_DefaultNamespace_ProducesSameGraphAsBpmnPrefix()
		{
			var expected = Describe(_parser.Parse(BpmnFixtures.WorkedExample));

			var graph = _parser.Parse(BpmnFixtures.Load("Parsing/default-namespace.bpmn"));

			Assert.Equal(expected, Describe(graph));
		}

		[Fact]
		public void Parse_ElementsInForeignNamespace_AreIgnored()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Raw("<x:task xmlns:x=\"urn:foreign\" id=\"Foreign\" />")
				.Build();

			var graph = _parser.Parse(xml);

			var node = Assert.Single(graph.Nodes);
			Assert.Equal("Start", node.BpmnId);
			Assert.Empty(graph.ParseIssues);
		}

		[Fact]
		public void Parse_BpmnJsGeneratedFile_ParsesAllTaskTypes()
		{
			var xml = BpmnFixtures.Load("Parsing/bpmn-js-generated.bpmn");

			var graph = _parser.Parse(xml);

			Assert.Equal("Process_0xyz789", graph.ProcessId);
			Assert.Equal(13, graph.Nodes.Count);
			Assert.Equal(12, graph.Edges.Count);
			Assert.Empty(graph.ParseIssues);
			Assert.All(new[] { "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8" }, id => Assert.Equal(EFlowNodeKind.Task, graph.NodesById[id].Kind));
			Assert.Equal("receiveTask", graph.NodesById["T8"].BpmnType);
			Assert.Equal(8m, graph.NodesById["T8"].Duration);
			Assert.Equal(EFlowNodeKind.CallActivity, graph.NodesById["Call"].Kind);
		}

		[Theory]
		[InlineData("startEvent", EFlowNodeKind.StartEvent)]
		[InlineData("endEvent", EFlowNodeKind.EndEvent)]
		[InlineData("task", EFlowNodeKind.Task)]
		[InlineData("userTask", EFlowNodeKind.Task)]
		[InlineData("manualTask", EFlowNodeKind.Task)]
		[InlineData("serviceTask", EFlowNodeKind.Task)]
		[InlineData("scriptTask", EFlowNodeKind.Task)]
		[InlineData("businessRuleTask", EFlowNodeKind.Task)]
		[InlineData("sendTask", EFlowNodeKind.Task)]
		[InlineData("receiveTask", EFlowNodeKind.Task)]
		[InlineData("subProcess", EFlowNodeKind.SubProcess)]
		[InlineData("callActivity", EFlowNodeKind.CallActivity)]
		[InlineData("intermediateCatchEvent", EFlowNodeKind.IntermediateEvent)]
		[InlineData("intermediateThrowEvent", EFlowNodeKind.IntermediateEvent)]
		[InlineData("exclusiveGateway", EFlowNodeKind.ExclusiveGateway)]
		[InlineData("parallelGateway", EFlowNodeKind.ParallelGateway)]
		[InlineData("inclusiveGateway", EFlowNodeKind.InclusiveGateway)]
		[InlineData("eventBasedGateway", EFlowNodeKind.EventBasedGateway)]
		public void Parse_SupportedElement_MapsToKind(string localName, EFlowNodeKind expectedKind)
		{
			var xml = new BpmnDiagramBuilder().Node(localName, "N").Build();

			var graph = _parser.Parse(xml);

			var node = Assert.Single(graph.Nodes);
			Assert.Equal(expectedKind, node.Kind);
			Assert.Equal(localName, node.BpmnType);
			Assert.Empty(graph.ParseIssues);
		}

		[Fact]
		public void Parse_DecimalWithTrailingZero_IsNormalised()
		{
			var xml = new BpmnDiagramBuilder().Raw("<bpmn:task id=\"T\" po:duration=\"5.50\" po:cost=\"1000.00\" />").Build();

			var graph = _parser.Parse(xml);

			var node = graph.NodesById["T"];
			Assert.Equal("5.5", node.Duration.ToString(CultureInfo.InvariantCulture));
			Assert.Equal("1000", node.Cost.ToString(CultureInfo.InvariantCulture));
		}

		[Fact]
		public void Parse_UnderCommaDecimalCulture_ReadsDotDecimal()
		{
			var xml = new BpmnDiagramBuilder().Raw("<bpmn:task id=\"T\" po:duration=\"2.5\" po:cost=\"1234.75\" />").Build();
			var originalCulture = CultureInfo.CurrentCulture;
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

			ProcessGraph graph;
			try
			{
				graph = _parser.Parse(xml);
			}
			finally
			{
				CultureInfo.CurrentCulture = originalCulture;
			}

			Assert.Equal(2.5m, graph.NodesById["T"].Duration);
			Assert.Equal(1234.75m, graph.NodesById["T"].Cost);
			Assert.Empty(graph.ParseIssues);
		}

		[Fact]
		public void Parse_XxePayload_ThrowsInvalidRequest()
		{
			var xml = BpmnFixtures.Load("Parsing/xxe.bpmn");

			var exception = Assert.Throws<InvalidRequestException>(() => _parser.Parse(xml));

			Assert.Equal(NotWellFormedMessage, exception.Message);
		}

		[Fact]
		public void Parse_BillionLaughsPayload_ThrowsInvalidRequest()
		{
			var xml = BpmnFixtures.Load("Parsing/billion-laughs.bpmn");

			var exception = Assert.Throws<InvalidRequestException>(() => _parser.Parse(xml));

			Assert.Equal(NotWellFormedMessage, exception.Message);
		}

		[Fact]
		public void Parse_DoctypeWithoutEntities_ThrowsInvalidRequest()
		{
			var xml = DefaultDiagram.Xml.Replace("<bpmn:definitions", "<!DOCTYPE bpmn:definitions>\n<bpmn:definitions", StringComparison.Ordinal);

			var exception = Assert.Throws<InvalidRequestException>(() => _parser.Parse(xml));

			Assert.Equal(NotWellFormedMessage, exception.Message);
		}

		[Theory]
		[InlineData("")]
		[InlineData("not xml at all")]
		[InlineData("<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"><bpmn:process id=\"P\">")]
		[InlineData("<definitions><process></definitions>")]
		public void Parse_MalformedXml_ThrowsInvalidRequest(string xml)
		{
			var exception = Assert.Throws<InvalidRequestException>(() => _parser.Parse(xml));

			Assert.Equal(NotWellFormedMessage, exception.Message);
		}

		[Theory]
		[InlineData("<root />")]
		[InlineData("<definitions />")]
		[InlineData("<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/DI\" />")]
		[InlineData("<bpmn:process xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\" id=\"P\" />")]
		public void Parse_NonBpmnRoot_ThrowsInvalidRequest(string xml)
		{
			var exception = Assert.Throws<InvalidRequestException>(() => _parser.Parse(xml));

			Assert.Equal(InvalidRootMessage, exception.Message);
		}

		[Fact]
		public void Parse_XmlLargerThanTwoMegabytes_ThrowsInvalidRequest()
		{
			var xml = PadToByteCount(DefaultDiagram.Xml, AnalysisLimits.MaxXmlBytes + 1);

			var exception = Assert.Throws<InvalidRequestException>(() => _parser.Parse(xml));

			Assert.Equal(XmlTooLargeMessage, exception.Message);
		}

		[Fact]
		public void Parse_XmlOfExactlyTwoMegabytes_Succeeds()
		{
			var xml = PadToByteCount(DefaultDiagram.Xml, AnalysisLimits.MaxXmlBytes);

			var graph = _parser.Parse(xml);

			Assert.Single(graph.Nodes);
		}

		[Fact]
		public void Parse_MultiByteCharactersAboveLimit_ThrowsInvalidRequest()
		{
			var name = new string('é', AnalysisLimits.MaxXmlBytes / 2);
			var xml = new BpmnDiagramBuilder().StartEvent("Start", name).Build();

			var exception = Assert.Throws<InvalidRequestException>(() => _parser.Parse(xml));

			Assert.Equal(XmlTooLargeMessage, exception.Message);
		}

		[Fact]
		public void Parse_OversizedMalformedXml_ReportsSizeBeforeParsing()
		{
			var xml = "<" + new string('x', AnalysisLimits.MaxXmlBytes);

			var exception = Assert.Throws<InvalidRequestException>(() => _parser.Parse(xml));

			Assert.Equal(XmlTooLargeMessage, exception.Message);
		}

		[Fact]
		public void Parse_MoreThanThousandFlowNodes_ThrowsInvalidRequest()
		{
			var xml = BpmnDiagramBuilder.Chain(AnalysisLimits.MaxFlowNodes - 1);

			var exception = Assert.Throws<InvalidRequestException>(() => _parser.Parse(xml));

			Assert.Equal(TooManyFlowNodesMessage, exception.Message);
		}

		[Fact]
		public void Parse_ExactlyThousandFlowNodes_Succeeds()
		{
			var xml = BpmnDiagramBuilder.Chain(AnalysisLimits.MaxFlowNodes - 2);

			var graph = _parser.Parse(xml);

			Assert.Equal(AnalysisLimits.MaxFlowNodes, graph.FlowNodeCount);
			Assert.Equal(AnalysisLimits.MaxFlowNodes, graph.Nodes.Count);
			Assert.Empty(graph.ParseIssues);
		}

		[Fact]
		public void Parse_NestedFlowNodesBeyondLimit_ThrowsInvalidRequest()
		{
			var innerTasks = string.Concat(Enumerable.Range(1, 10).Select(index => $"<bpmn:task id=\"Inner{index}\" />"));
			var xml = new BpmnDiagramBuilder()
				.Raw(string.Concat(Enumerable.Range(1, AnalysisLimits.MaxFlowNodes - 10).Select(index => $"<bpmn:task id=\"T{index}\" />")))
				.Raw($"<bpmn:subProcess id=\"Sub\">{innerTasks}</bpmn:subProcess>")
				.Build();

			var exception = Assert.Throws<InvalidRequestException>(() => _parser.Parse(xml));

			Assert.Equal(TooManyFlowNodesMessage, exception.Message);
		}

		[Fact]
		public void Parse_TwoProcesses_ReportsDv01()
		{
			var xml = Definitions("<bpmn:process id=\"P1\"><bpmn:startEvent id=\"S1\" /></bpmn:process><bpmn:process id=\"P2\"><bpmn:startEvent id=\"S2\" /></bpmn:process>");

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.ProcessCount);
			Assert.Null(issue.BpmnElementId);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.Equal("The diagram must contain exactly one process.", issue.Message);
			Assert.Equal("P1", graph.ProcessId);
			Assert.Equal("S1", Assert.Single(graph.Nodes).BpmnId);
			Assert.Equal(2, graph.FlowNodeCount);
		}

		[Fact]
		public void Parse_NoProcess_ReportsDv01AndReturnsEmptyGraph()
		{
			var xml = Definitions(string.Empty);

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.ProcessCount);
			Assert.Null(issue.BpmnElementId);
			Assert.Equal(string.Empty, graph.ProcessId);
			Assert.Empty(graph.Nodes);
			Assert.Empty(graph.Edges);
		}

		[Fact]
		public void Parse_EmptyProcess_ReportsDv01()
		{
			var xml = new BpmnDiagramBuilder().Raw("<bpmn:laneSet id=\"Lanes\" />").Build();

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.ProcessCount);
			Assert.Equal(BpmnDiagramBuilder.ProcessId, issue.BpmnElementId);
			Assert.Equal("The process must not be empty.", issue.Message);
			Assert.Empty(graph.Nodes);
		}

		[Fact]
		public void Parse_ComplexGateway_ReportsDv04()
		{
			var xml = new BpmnDiagramBuilder().StartEvent("Start").Node("complexGateway", "Complex").Build();

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.UnsupportedElement);
			Assert.Equal("Complex", issue.BpmnElementId);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.Equal("The element type 'complexGateway' is not supported.", issue.Message);
			Assert.False(graph.NodesById.ContainsKey("Complex"));
		}

		[Theory]
		[InlineData("boundaryEvent")]
		[InlineData("transaction")]
		[InlineData("adHocSubProcess")]
		public void Parse_UnsupportedElement_ReportsDv04AndSkipsNode(string localName)
		{
			var xml = new BpmnDiagramBuilder().StartEvent("Start").Node(localName, "Unsupported").Build();

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.UnsupportedElement);
			Assert.Equal("Unsupported", issue.BpmnElementId);
			Assert.Equal($"The element type '{localName}' is not supported.", issue.Message);
			Assert.Equal("Start", Assert.Single(graph.Nodes).BpmnId);
		}

		[Fact]
		public void Parse_EventSubProcess_ReportsDv04AndSkipsNode()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Raw("<bpmn:subProcess id=\"EventSub\" triggeredByEvent=\"true\"><bpmn:startEvent id=\"InnerStart\" /></bpmn:subProcess>")
				.Build();

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.UnsupportedElement);
			Assert.Equal("EventSub", issue.BpmnElementId);
			Assert.Equal("The element type 'subProcess' is not supported.", issue.Message);
			Assert.False(graph.NodesById.ContainsKey("EventSub"));
		}

		[Fact]
		public void Parse_DanglingSequenceFlow_ReportsDv05AndDropsEdge()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.EndEvent("End")
				.Flow("Good", "Start", "End")
				.Flow("Dangling", "Start", "Missing")
				.Build();

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.DanglingSequenceFlow);
			Assert.Equal("Dangling", issue.BpmnElementId);
			Assert.Equal(EIssueSeverity.Error, issue.Severity);
			Assert.Equal("The sequence flow references the missing or unsupported flow node 'Missing'.", issue.Message);
			Assert.Equal("Good", Assert.Single(graph.Edges).BpmnId);
			Assert.Equal(new[] { "End" }, graph.NodesById["Start"].Dependents);
		}

		[Fact]
		public void Parse_SequenceFlowWithBothEndsMissing_ReportsOneDv05PerReference()
		{
			var xml = new BpmnDiagramBuilder().StartEvent("Start").Flow("Dangling", "MissingA", "MissingB").Build();

			var graph = _parser.Parse(xml);

			var issues = graph.ParseIssues.Where(issue => issue.Code == DiagramIssueCodes.DanglingSequenceFlow).ToList();
			Assert.Equal(2, issues.Count);
			Assert.All(issues, issue => Assert.Equal("Dangling", issue.BpmnElementId));
			Assert.Empty(graph.Edges);
		}

		[Fact]
		public void Parse_SequenceFlowWithSameMissingReferenceTwice_ReportsOneDv05()
		{
			var xml = new BpmnDiagramBuilder().StartEvent("Start").Flow("Loop", "Missing", "Missing").Build();

			var graph = _parser.Parse(xml);

			SingleIssue(graph, DiagramIssueCodes.DanglingSequenceFlow);
			Assert.Empty(graph.Edges);
		}

		[Fact]
		public void Parse_SequenceFlowToUnsupportedElement_ReportsDv04AndDv05()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Node("complexGateway", "Complex")
				.Flow("ToComplex", "Start", "Complex")
				.Build();

			var graph = _parser.Parse(xml);

			SingleIssue(graph, DiagramIssueCodes.UnsupportedElement);
			var issue = SingleIssue(graph, DiagramIssueCodes.DanglingSequenceFlow);
			Assert.Equal("ToComplex", issue.BpmnElementId);
			Assert.Empty(graph.Edges);
		}

		[Fact]
		public void Parse_NegativeDuration_ReportsDv09AndTreatsAsMissing()
		{
			var xml = new BpmnDiagramBuilder().Raw("<bpmn:task id=\"T\" po:duration=\"-1\" po:cost=\"100\" />").Build();

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.InvalidValue);
			Assert.Equal("T", issue.BpmnElementId);
			Assert.Equal("The value '-1' of po:duration must be a finite number greater than or equal to 0.", issue.Message);
			Assert.Equal(0m, graph.NodesById["T"].Duration);
			Assert.False(graph.NodesById["T"].HasDuration);
			Assert.True(graph.NodesById["T"].HasCost);
		}

		[Theory]
		[InlineData("NaN")]
		[InlineData("Infinity")]
		[InlineData("abc")]
		[InlineData("1,5")]
		public void Parse_NaNCost_ReportsDv09(string rawCost)
		{
			var xml = new BpmnDiagramBuilder().Raw($"<bpmn:task id=\"T\" po:duration=\"1\" po:cost=\"{rawCost}\" />").Build();

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.InvalidValue);
			Assert.Equal($"The value '{rawCost}' of po:cost must be a finite number greater than or equal to 0.", issue.Message);
			Assert.Equal(0m, graph.NodesById["T"].Cost);
			Assert.False(graph.NodesById["T"].HasCost);
		}

		[Fact]
		public void Parse_MissingDurationAndCost_DefaultsToZeroWithoutIssue()
		{
			var xml = new BpmnDiagramBuilder().Task("T", "Work", null, null).Build();

			var graph = _parser.Parse(xml);

			var node = graph.NodesById["T"];
			Assert.Equal(0m, node.Duration);
			Assert.Equal(0m, node.Cost);
			Assert.False(node.HasDuration);
			Assert.False(node.HasCost);
			Assert.Empty(graph.ParseIssues);
		}

		[Fact]
		public void Parse_ZeroDurationAndCost_AreAccepted()
		{
			var xml = new BpmnDiagramBuilder().Task("T", "Work", 0, 0).Build();

			var graph = _parser.Parse(xml);

			Assert.True(graph.NodesById["T"].HasDuration);
			Assert.True(graph.NodesById["T"].HasCost);
			Assert.Empty(graph.ParseIssues);
		}

		[Fact]
		public void Parse_InvalidParallelizable_ReportsDv09()
		{
			var xml = new BpmnDiagramBuilder().Raw("<bpmn:task id=\"T\" po:duration=\"1\" po:cost=\"1\" po:parallelizable=\"maybe\" />").Build();

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.InvalidValue);
			Assert.Equal("T", issue.BpmnElementId);
			Assert.Equal("The value 'maybe' of po:parallelizable must be 'true' or 'false'.", issue.Message);
			Assert.True(graph.NodesById["T"].Parallelizable);
		}

		[Fact]
		public void Parse_ParallelizableOnGateway_IsIgnored()
		{
			var xml = new BpmnDiagramBuilder()
				.Raw("<bpmn:parallelGateway id=\"G\" po:parallelizable=\"false\" />")
				.Raw("<bpmn:exclusiveGateway id=\"X\" po:parallelizable=\"maybe\" />")
				.Build();

			var graph = _parser.Parse(xml);

			Assert.True(graph.NodesById["G"].Parallelizable);
			Assert.True(graph.NodesById["X"].Parallelizable);
			Assert.Empty(graph.ParseIssues);
		}

		[Fact]
		public void Parse_ValidEstimate_IsRead()
		{
			var xml = TaskWithExtensions("<po:estimate optimistic=\"3\" mostLikely=\"5\" pessimistic=\"9.50\" />");

			var graph = _parser.Parse(xml);

			Assert.Equal(new Estimate(3m, 5m, 9.5m), graph.NodesById["T"].Estimate);
			Assert.Empty(graph.ParseIssues);
		}

		[Fact]
		public void Parse_EqualEstimateValues_AreAccepted()
		{
			var xml = TaskWithExtensions("<po:estimate optimistic=\"5\" mostLikely=\"5\" pessimistic=\"5\" />");

			var graph = _parser.Parse(xml);

			Assert.Equal(new Estimate(5m, 5m, 5m), graph.NodesById["T"].Estimate);
			Assert.Empty(graph.ParseIssues);
		}

		[Theory]
		[InlineData("optimistic=\"6\" mostLikely=\"5\" pessimistic=\"9\"")]
		[InlineData("optimistic=\"3\" mostLikely=\"10\" pessimistic=\"9\"")]
		[InlineData("optimistic=\"3\" pessimistic=\"9\"")]
		[InlineData("optimistic=\"-1\" mostLikely=\"5\" pessimistic=\"9\"")]
		public void Parse_UnorderedEstimate_ReportsDv09(string attributes)
		{
			var xml = TaskWithExtensions($"<po:estimate {attributes} />");

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.InvalidValue);
			Assert.Equal("T", issue.BpmnElementId);
			Assert.Equal(UnorderedEstimateMessage, issue.Message);
			Assert.Null(graph.NodesById["T"].Estimate);
		}

		[Fact]
		public void Parse_ValidCrashOption_IsRead()
		{
			var xml = TaskWithExtensions("<po:crashOption duration=\"3\" cost=\"1500\" /><po:crashOption duration=\"2\" cost=\"1000\" />");

			var graph = _parser.Parse(xml);

			Assert.Equal(new[] { new CrashOption(3m, 1500m), new CrashOption(2m, 1000m) }, graph.NodesById["T"].CrashOptions);
			Assert.Empty(graph.ParseIssues);
		}

		[Theory]
		[InlineData("duration=\"5\" cost=\"2000\"")]
		[InlineData("duration=\"6\" cost=\"2000\"")]
		[InlineData("duration=\"3\" cost=\"999\"")]
		[InlineData("duration=\"3\"")]
		[InlineData("duration=\"abc\" cost=\"2000\"")]
		public void Parse_CrashOptionNotFaster_ReportsDv09(string attributes)
		{
			var xml = TaskWithExtensions($"<po:crashOption {attributes} /><po:crashOption duration=\"4\" cost=\"1200\" />");

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.InvalidValue);
			Assert.Equal("T", issue.BpmnElementId);
			Assert.Equal(InvalidCrashOptionMessage, issue.Message);
			Assert.Equal(new[] { new CrashOption(4m, 1200m) }, graph.NodesById["T"].CrashOptions);
		}

		[Fact]
		public void Parse_ExpandedSubProcess_ReportsDv16Warning()
		{
			var xml = new BpmnDiagramBuilder()
				.Raw("<bpmn:subProcess id=\"Sub\" po:duration=\"3\" po:cost=\"10\"><bpmn:startEvent id=\"InnerStart\" /><bpmn:task id=\"InnerTask\" /><bpmn:sequenceFlow id=\"InnerFlow\" sourceRef=\"InnerStart\" targetRef=\"InnerTask\" /></bpmn:subProcess>")
				.Build();

			var graph = _parser.Parse(xml);

			var issue = SingleIssue(graph, DiagramIssueCodes.IgnoredSubProcessContent);
			Assert.Equal(EIssueSeverity.Warning, issue.Severity);
			Assert.Equal("Sub", issue.BpmnElementId);
			Assert.Equal("The inner elements of the expanded sub-process are ignored.", issue.Message);
			var node = Assert.Single(graph.Nodes);
			Assert.Equal(EFlowNodeKind.SubProcess, node.Kind);
			Assert.Equal(3m, node.Duration);
			Assert.Empty(graph.Edges);
		}

		[Fact]
		public void Parse_CollapsedSubProcess_ReportsNoIssue()
		{
			var xml = new BpmnDiagramBuilder()
				.StartEvent("Start")
				.Raw("<bpmn:subProcess id=\"Sub\"><bpmn:incoming>F1</bpmn:incoming><bpmn:documentation>Notes</bpmn:documentation></bpmn:subProcess>")
				.Flow("F1", "Start", "Sub")
				.Build();

			var graph = _parser.Parse(xml);

			Assert.Empty(graph.ParseIssues);
			Assert.Equal(EFlowNodeKind.SubProcess, graph.NodesById["Sub"].Kind);
		}

		[Fact]
		public void Parse_DefaultFlow_MarksEdgeAsDefault()
		{
			var xml = new BpmnDiagramBuilder()
				.Gateway("Split", EFlowNodeKind.ExclusiveGateway, "FlowB")
				.Task("A", "A", 1, 1)
				.Task("B", "B", 1, 1)
				.Flow("FlowA", "Split", "A", "x > 1")
				.Flow("FlowB", "Split", "B")
				.Build();

			var graph = _parser.Parse(xml);

			Assert.False(graph.Edges.Single(edge => edge.BpmnId == "FlowA").IsDefault);
			Assert.True(graph.Edges.Single(edge => edge.BpmnId == "FlowB").IsDefault);
		}

		[Fact]
		public void Parse_InconsistentIncomingOutgoingChildren_AreIgnored()
		{
			var xml = new BpmnDiagramBuilder()
				.Raw("<bpmn:startEvent id=\"Start\"><bpmn:outgoing>Nowhere</bpmn:outgoing></bpmn:startEvent>")
				.Raw("<bpmn:endEvent id=\"End\"><bpmn:incoming>Elsewhere</bpmn:incoming></bpmn:endEvent>")
				.Flow("F1", "Start", "End")
				.Build();

			var graph = _parser.Parse(xml);

			Assert.Empty(graph.ParseIssues);
			Assert.Equal("F1", Assert.Single(graph.Edges).BpmnId);
			Assert.Equal(new[] { "Start" }, graph.NodesById["End"].DependsOn);
		}

		[Fact]
		public void Parse_EventBasedGateway_IsNotControllable()
		{
			var xml = new BpmnDiagramBuilder()
				.Gateway("Event", EFlowNodeKind.EventBasedGateway)
				.Gateway("Exclusive", EFlowNodeKind.ExclusiveGateway)
				.Gateway("Inclusive", EFlowNodeKind.InclusiveGateway)
				.Build();

			var graph = _parser.Parse(xml);

			Assert.False(graph.NodesById["Event"].IsControllable);
			Assert.True(graph.NodesById["Exclusive"].IsControllable);
			Assert.True(graph.NodesById["Inclusive"].IsControllable);
		}

		[Fact]
		public void Parse_GatewayWithManyIncomingAndOutgoing_HasNoDirection()
		{
			var xml = new BpmnDiagramBuilder()
				.Task("A", "A", 1, 1)
				.Task("B", "B", 1, 1)
				.Gateway("Mixed", EFlowNodeKind.ParallelGateway)
				.Task("C", "C", 1, 1)
				.Task("D", "D", 1, 1)
				.Flow("F1", "A", "Mixed")
				.Flow("F2", "B", "Mixed")
				.Flow("F3", "Mixed", "C")
				.Flow("F4", "Mixed", "D")
				.Build();

			var graph = _parser.Parse(xml);

			Assert.Equal(EGatewayDirection.None, graph.NodesById["Mixed"].GatewayDirection);
		}

		[Fact]
		public void Parse_ConditionExpression_IsKeptAsText()
		{
			var xml = new BpmnDiagramBuilder()
				.Gateway("Split", EFlowNodeKind.ExclusiveGateway)
				.Task("A", "A", 1, 1)
				.Task("B", "B", 1, 1)
				.Flow("FlowA", "Split", "A", "${amount > 1000 && approved}")
				.Flow("FlowB", "Split", "B")
				.Build();

			var graph = _parser.Parse(xml);

			Assert.Equal("${amount > 1000 && approved}", graph.Edges.Single(edge => edge.BpmnId == "FlowA").ConditionExpression);
			Assert.Null(graph.Edges.Single(edge => edge.BpmnId == "FlowB").ConditionExpression);
		}

		[Fact]
		public void Parse_MissingName_DefaultsToEmptyString()
		{
			var xml = new BpmnDiagramBuilder().StartEvent("Start").EndEvent("End").Flow("F1", "Start", "End").Build();

			var graph = _parser.Parse(xml);

			Assert.Equal(string.Empty, graph.NodesById["Start"].Name);
			Assert.Equal(string.Empty, Assert.Single(graph.Edges).Name);
		}

		[Fact]
		public void Parse_DuplicateFlowNodeId_KeepsFirstNode()
		{
			var xml = new BpmnDiagramBuilder().Task("T", "First", 1, 1).Task("T", "Second", 2, 2).Build();

			var graph = _parser.Parse(xml);

			var node = Assert.Single(graph.Nodes);
			Assert.Equal("First", node.Name);
		}

		private static ValidationIssue SingleIssue(ProcessGraph graph, string code) =>
			Assert.Single(graph.ParseIssues, issue => issue.Code == code);

		private static string TaskWithExtensions(string extensions) =>
			new BpmnDiagramBuilder()
				.Raw($"<bpmn:task id=\"T\" name=\"Work\" po:duration=\"5\" po:cost=\"1000\"><bpmn:extensionElements>{extensions}</bpmn:extensionElements></bpmn:task>")
				.Build();

		private static string Definitions(string content) =>
			$"<bpmn:definitions xmlns:bpmn=\"{BpmnNamespaces.Model}\" xmlns:po=\"{BpmnNamespaces.ProjectOptimizer}\" id=\"Definitions_1\">{content}</bpmn:definitions>";

		private static string PadToByteCount(string xml, int byteCount) =>
			xml + new string(' ', byteCount - Encoding.UTF8.GetByteCount(xml));

		private static List<string> Describe(ProcessGraph graph) =>
			graph.Nodes
				.Select(node => string.Join('|', node.BpmnId, node.Name, node.Kind, node.Duration, node.Cost, node.Parallelizable,
					node.GatewayDirection, string.Join(',', node.DependsOn), string.Join(',', node.Dependents)))
				.Concat(graph.Edges.Select(edge => string.Join('|', edge.BpmnId, edge.SourceId, edge.TargetId, edge.IsDefault)))
				.Append(graph.ProcessId)
				.ToList();
	}
}
