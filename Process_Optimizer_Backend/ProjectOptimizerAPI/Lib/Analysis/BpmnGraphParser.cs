using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed class BpmnGraphParser : IBpmnGraphParser
	{
		private static readonly string XmlTooLargeMessage = string.Create(CultureInfo.InvariantCulture, $"The diagram XML must not be larger than {AnalysisLimits.MaxXmlMegabytes} MB.");
		private const string NotWellFormedMessage = "The diagram is not well-formed XML or contains a DTD.";
		private const string InvalidRootMessage = "The diagram root element must be a BPMN 2.0 definitions element.";
		private static readonly string TooManyFlowNodesMessage = string.Create(CultureInfo.InvariantCulture, $"The diagram must not contain more than {AnalysisLimits.MaxFlowNodes} flow nodes.");
		private const string ProcessCountMessage = "The diagram must contain exactly one process.";
		private const string EmptyProcessMessage = "The process must not be empty.";
		private const string UnorderedEstimateMessage = "The estimate must satisfy optimistic <= mostLikely <= pessimistic.";
		private const string InvalidCrashOptionMessage = "A crash option must be faster than the normal duration and not cheaper than the normal cost.";
		private const string IgnoredSubProcessContentMessage = "The inner elements of the expanded sub-process are ignored.";
		private const string TrueValue = "true";
		private const string FalseValue = "false";

		private static readonly XNamespace ModelNamespace = BpmnNamespaces.Model;
		private static readonly XNamespace ProjectOptimizerNamespace = BpmnNamespaces.ProjectOptimizer;

		private static readonly FrozenDictionary<string, EFlowNodeKind> SupportedKinds = new Dictionary<string, EFlowNodeKind>
		{
			["startEvent"] = EFlowNodeKind.StartEvent,
			["endEvent"] = EFlowNodeKind.EndEvent,
			["task"] = EFlowNodeKind.Task,
			["userTask"] = EFlowNodeKind.Task,
			["manualTask"] = EFlowNodeKind.Task,
			["serviceTask"] = EFlowNodeKind.Task,
			["scriptTask"] = EFlowNodeKind.Task,
			["businessRuleTask"] = EFlowNodeKind.Task,
			["sendTask"] = EFlowNodeKind.Task,
			["receiveTask"] = EFlowNodeKind.Task,
			["subProcess"] = EFlowNodeKind.SubProcess,
			["callActivity"] = EFlowNodeKind.CallActivity,
			["intermediateCatchEvent"] = EFlowNodeKind.IntermediateEvent,
			["intermediateThrowEvent"] = EFlowNodeKind.IntermediateEvent,
			["exclusiveGateway"] = EFlowNodeKind.ExclusiveGateway,
			["parallelGateway"] = EFlowNodeKind.ParallelGateway,
			["inclusiveGateway"] = EFlowNodeKind.InclusiveGateway,
			["eventBasedGateway"] = EFlowNodeKind.EventBasedGateway
		}.ToFrozenDictionary(StringComparer.Ordinal);

		private static readonly FrozenSet<string> UnsupportedLocalNames = new[]
		{
			"complexGateway", "boundaryEvent", "transaction", "adHocSubProcess"
		}.ToFrozenSet(StringComparer.Ordinal);

		private static readonly FrozenSet<string> FlowNodeLocalNames = SupportedKinds.Keys
			.Concat(UnsupportedLocalNames)
			.Append("implicitThrowEvent")
			.ToFrozenSet(StringComparer.Ordinal);

		private static readonly FrozenSet<string> SubProcessContentLocalNames = FlowNodeLocalNames
			.Concat(["sequenceFlow", "laneSet", "dataObject", "dataObjectReference", "dataStoreReference", "textAnnotation", "association", "group"])
			.ToFrozenSet(StringComparer.Ordinal);

		public ProcessGraph Parse(string bpmnXml)
		{
			EnsureWithinSizeLimit(bpmnXml);
			var document = LoadDocument(bpmnXml);
			EnsureBpmnRoot(document);
			var processes = document.Root!.Elements(ModelNamespace + "process").ToList();
			var flowNodeCount = CountFlowNodes(processes);
			EnsureWithinFlowNodeLimit(flowNodeCount);
			var reader = new ProcessReader(flowNodeCount);
			return processes.Count == 1 ? reader.Read(processes[0]) : reader.ReadWithWrongProcessCount(processes);
		}

		private static void EnsureWithinSizeLimit(string bpmnXml)
		{
			if (Encoding.UTF8.GetByteCount(bpmnXml) > AnalysisLimits.MaxXmlBytes)
			{
				throw new InvalidRequestException(XmlTooLargeMessage);
			}
		}

		private static XDocument LoadDocument(string bpmnXml)
		{
			var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
			try
			{
				using var stringReader = new StringReader(bpmnXml);
				using var xmlReader = XmlReader.Create(stringReader, settings);
				return XDocument.Load(xmlReader);
			}
			catch (XmlException)
			{
				throw new InvalidRequestException(NotWellFormedMessage);
			}
		}

		private static void EnsureBpmnRoot(XDocument document)
		{
			if (document.Root?.Name != ModelNamespace + "definitions")
			{
				throw new InvalidRequestException(InvalidRootMessage);
			}
		}

		private static void EnsureWithinFlowNodeLimit(int flowNodeCount)
		{
			if (flowNodeCount > AnalysisLimits.MaxFlowNodes)
			{
				throw new InvalidRequestException(TooManyFlowNodesMessage);
			}
		}

		private static int CountFlowNodes(IEnumerable<XElement> processes) =>
			processes.SelectMany(process => process.Descendants()).Count(IsFlowNodeElement);

		private static bool IsFlowNodeElement(XElement element) =>
			element.Name.Namespace == ModelNamespace && FlowNodeLocalNames.Contains(element.Name.LocalName);

		private static string IdOf(XElement element) => (string?)element.Attribute("id") ?? string.Empty;

		private static string NameOf(XElement element) => (string?)element.Attribute("name") ?? string.Empty;

		private static bool IsTrue(string? value) => string.Equals(value?.Trim(), TrueValue, StringComparison.Ordinal);

		private static bool IsEventSubProcess(XElement element) =>
			element.Name.LocalName == "subProcess" && IsTrue((string?)element.Attribute("triggeredByEvent"));

		private static bool IsUnsupported(XElement element) =>
			element.Name.Namespace == ModelNamespace
			&& (UnsupportedLocalNames.Contains(element.Name.LocalName) || IsEventSubProcess(element));

		private static bool TryGetSupportedKind(XElement element, out EFlowNodeKind kind)
		{
			kind = default;
			return element.Name.Namespace == ModelNamespace
				&& !IsEventSubProcess(element)
				&& SupportedKinds.TryGetValue(element.Name.LocalName, out kind);
		}

		private static decimal? ParseAmount(string? raw)
		{
			if (raw is null || !decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0)
			{
				return null;
			}

			return DecimalNormalizer.Normalize(value);
		}

		private static IReadOnlyList<string> DistinctSorted(IEnumerable<string> ids) =>
			ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

		private static EGatewayDirection DirectionOf(FlowNode node, int incomingCount, int outgoingCount)
		{
			if (!node.IsGateway)
			{
				return EGatewayDirection.None;
			}

			if (incomingCount <= 1 && outgoingCount >= 2)
			{
				return EGatewayDirection.Split;
			}

			return incomingCount >= 2 && outgoingCount <= 1 ? EGatewayDirection.Join : EGatewayDirection.None;
		}

		private sealed class ProcessReader(int flowNodeCount)
		{
			private readonly List<ValidationIssue> _issues = [];

			public ProcessGraph ReadWithWrongProcessCount(IReadOnlyList<XElement> processes)
			{
				ReportError(DiagramIssueCodes.ProcessCount, null, ProcessCountMessage);
				return processes.Count == 0 ? CreateGraph(string.Empty, [], []) : Read(processes[0]);
			}

			public ProcessGraph Read(XElement process)
			{
				var nodeElements = SelectNodeElements(process);
				ReportEmptyProcess(process, nodeElements);
				var elementsById = nodeElements.ToDictionary(IdOf, StringComparer.Ordinal);
				var edges = ReadEdges(process, elementsById);
				var nodes = nodeElements.Select(ReadNode).Select(node => WithDependencies(node, edges)).ToList();
				return CreateGraph(IdOf(process), nodes, edges);
			}

			private ProcessGraph CreateGraph(string processId, IReadOnlyList<FlowNode> nodes, IReadOnlyList<SequenceFlowEdge> edges) =>
				new(processId, nodes, edges, nodes.ToDictionary(node => node.BpmnId, StringComparer.Ordinal), flowNodeCount, _issues);

			private List<XElement> SelectNodeElements(XElement process)
			{
				var nodeElements = new List<XElement>();
				var seenIds = new HashSet<string>(StringComparer.Ordinal);
				foreach (var element in process.Elements())
				{
					if (IsUnsupported(element))
					{
						ReportError(DiagramIssueCodes.UnsupportedElement, IdOf(element), $"The element type '{element.Name.LocalName}' is not supported.");
					}
					else if (TryGetSupportedKind(element, out _) && seenIds.Add(IdOf(element)))
					{
						nodeElements.Add(element);
					}
				}

				return nodeElements;
			}

			private void ReportEmptyProcess(XElement process, IReadOnlyCollection<XElement> nodeElements)
			{
				if (nodeElements.Count == 0)
				{
					ReportError(DiagramIssueCodes.ProcessCount, IdOf(process), EmptyProcessMessage);
				}
			}

			private List<SequenceFlowEdge> ReadEdges(XElement process, IReadOnlyDictionary<string, XElement> elementsById) =>
				process.Elements(ModelNamespace + "sequenceFlow")
					.Select(flow => ReadEdge(flow, elementsById))
					.OfType<SequenceFlowEdge>()
					.ToList();

			private SequenceFlowEdge? ReadEdge(XElement flow, IReadOnlyDictionary<string, XElement> elementsById)
			{
				var flowId = IdOf(flow);
				var sourceId = (string?)flow.Attribute("sourceRef") ?? string.Empty;
				var targetId = (string?)flow.Attribute("targetRef") ?? string.Empty;
				var missingReferences = new[] { sourceId, targetId }.Where(reference => !elementsById.ContainsKey(reference)).Distinct(StringComparer.Ordinal).ToList();
				foreach (var reference in missingReferences)
				{
					ReportError(DiagramIssueCodes.DanglingSequenceFlow, flowId, $"The sequence flow references the missing or unsupported flow node '{reference}'.");
				}

				if (missingReferences.Count > 0)
				{
					return null;
				}

				var isDefault = string.Equals((string?)elementsById[sourceId].Attribute("default"), flowId, StringComparison.Ordinal);
				return new SequenceFlowEdge(flowId, sourceId, targetId, NameOf(flow), ConditionOf(flow), isDefault);
			}

			private static string? ConditionOf(XElement flow) => flow.Element(ModelNamespace + "conditionExpression")?.Value;

			private FlowNode ReadNode(XElement element)
			{
				TryGetSupportedKind(element, out var kind);
				var id = IdOf(element);
				var duration = ReadAmount(element, id, "duration");
				var cost = ReadAmount(element, id, "cost");
				var extensions = element.Elements(ModelNamespace + "extensionElements").ToList();
				ReportSubProcessContent(element, kind, id);
				return new FlowNode(
					BpmnId: id,
					Name: NameOf(element),
					Kind: kind,
					BpmnType: element.Name.LocalName,
					Duration: duration ?? 0m,
					Cost: cost ?? 0m,
					Parallelizable: !FlowNode.IsActivityKind(kind) || ReadParallelizable(element, id),
					HasDuration: duration.HasValue,
					HasCost: cost.HasValue,
					GatewayDirection: EGatewayDirection.None,
					IsControllable: kind != EFlowNodeKind.EventBasedGateway,
					DependsOn: [],
					Dependents: [],
					Estimate: ReadEstimate(extensions, id),
					CrashOptions: ReadCrashOptions(extensions, id, duration ?? 0m, cost ?? 0m));
			}

			private static FlowNode WithDependencies(FlowNode node, IReadOnlyList<SequenceFlowEdge> edges)
			{
				var incoming = edges.Where(edge => edge.TargetId == node.BpmnId).ToList();
				var outgoing = edges.Where(edge => edge.SourceId == node.BpmnId).ToList();
				return node with
				{
					DependsOn = DistinctSorted(incoming.Select(edge => edge.SourceId)),
					Dependents = DistinctSorted(outgoing.Select(edge => edge.TargetId)),
					GatewayDirection = DirectionOf(node, incoming.Count, outgoing.Count)
				};
			}

			private decimal? ReadAmount(XElement element, string id, string attribute)
			{
				var raw = (string?)element.Attribute(ProjectOptimizerNamespace + attribute);
				var value = ParseAmount(raw);
				if (raw is not null && value is null)
				{
					ReportError(DiagramIssueCodes.InvalidValue, id, $"The value '{raw}' of po:{attribute} must be a finite number greater than or equal to 0.");
				}

				return value;
			}

			private bool ReadParallelizable(XElement element, string id)
			{
				var raw = (string?)element.Attribute(ProjectOptimizerNamespace + "parallelizable");
				var trimmed = raw?.Trim();
				if (raw is null || trimmed == TrueValue)
				{
					return true;
				}

				if (trimmed != FalseValue)
				{
					ReportError(DiagramIssueCodes.InvalidValue, id, $"The value '{raw}' of po:parallelizable must be 'true' or 'false'.");
					return true;
				}

				return false;
			}

			private Estimate? ReadEstimate(IEnumerable<XElement> extensions, string id)
			{
				var estimateElement = extensions.Elements(ProjectOptimizerNamespace + "estimate").FirstOrDefault();
				if (estimateElement is null)
				{
					return null;
				}

				var optimistic = ParseAmount((string?)estimateElement.Attribute("optimistic"));
				var mostLikely = ParseAmount((string?)estimateElement.Attribute("mostLikely"));
				var pessimistic = ParseAmount((string?)estimateElement.Attribute("pessimistic"));
				if (optimistic is null || mostLikely is null || pessimistic is null || optimistic > mostLikely || mostLikely > pessimistic)
				{
					ReportError(DiagramIssueCodes.InvalidValue, id, UnorderedEstimateMessage);
					return null;
				}

				return new Estimate(optimistic.Value, mostLikely.Value, pessimistic.Value);
			}

			private List<CrashOption> ReadCrashOptions(IEnumerable<XElement> extensions, string id, decimal normalDuration, decimal normalCost)
			{
				var crashOptions = new List<CrashOption>();
				foreach (var crashElement in extensions.Elements(ProjectOptimizerNamespace + "crashOption"))
				{
					var duration = ParseAmount((string?)crashElement.Attribute("duration"));
					var cost = ParseAmount((string?)crashElement.Attribute("cost"));
					if (duration is null || cost is null || duration >= normalDuration || cost < normalCost)
					{
						ReportError(DiagramIssueCodes.InvalidValue, id, InvalidCrashOptionMessage);
						continue;
					}

					crashOptions.Add(new CrashOption(duration.Value, cost.Value));
				}

				return crashOptions;
			}

			private void ReportSubProcessContent(XElement element, EFlowNodeKind kind, string id)
			{
				var hasContent = element.Elements().Any(child =>
					child.Name.Namespace == ModelNamespace && SubProcessContentLocalNames.Contains(child.Name.LocalName));
				if (kind == EFlowNodeKind.SubProcess && hasContent)
				{
					_issues.Add(new ValidationIssue(DiagramIssueCodes.IgnoredSubProcessContent, EIssueSeverity.Warning, id, IgnoredSubProcessContentMessage));
				}
			}

			private void ReportError(string code, string? bpmnElementId, string message) =>
				_issues.Add(new ValidationIssue(code, EIssueSeverity.Error, bpmnElementId, message));
		}
	}
}
