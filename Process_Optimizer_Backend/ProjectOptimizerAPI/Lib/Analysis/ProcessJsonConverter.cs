using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed partial class ProcessJsonConverter : IProcessJsonConverter
	{
		private const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
		private const string TargetNamespace = "http://bpmn.io/schema/bpmn";
		private const string FormalExpressionType = "bpmn:tFormalExpression";
		private const string DefaultBranchType = "nor";
		private const string DiagramElementSuffix = "_di";
		private const string DefinitionsId = "Definitions_1";
		private const string ProcessId = "Process_1";
		private const string DiagramId = "BPMNDiagram_1";
		private const string PlaneId = "BPMNPlane_1";

		private static readonly XNamespace ModelNamespace = BpmnNamespaces.Model;
		private static readonly XNamespace DiNamespace = BpmnNamespaces.Di;
		private static readonly XNamespace DiagramCommonNamespace = BpmnNamespaces.DiagramCommon;
		private static readonly XNamespace DiagramInterchangeNamespace = BpmnNamespaces.DiagramInterchange;
		private static readonly XNamespace XmlSchemaInstanceNamespace = BpmnNamespaces.XmlSchemaInstance;
		private static readonly XNamespace ProjectOptimizerNamespace = BpmnNamespaces.ProjectOptimizer;
		private static readonly XNamespace FrontendNamespace = BpmnNamespaces.Frontend;

		private static readonly FrozenSet<string> NonFlowTypes = new[]
		{
			"TextAnnotation", "Group", "DataObject", "DataObjectReference", "DataStoreReference", "Participant", "Lane", "LaneSet",
			"Association", "DataInputAssociation", "DataOutputAssociation", "MessageFlow", "SequenceFlow", "Process", "Collaboration"
		}.ToFrozenSet(StringComparer.Ordinal);

		public string ToBpmnXml(IReadOnlyList<ProcessStateDTO> states, IReadOnlyList<ProcessBranchDTO> branches, ETimeUnit timeUnit, string currency)
		{
			EnsureValidIds(states.Select(state => state.Id).Concat(branches.Select(branch => branch.Id)));
			EnsureValidTypes(states);
			var nodes = states.Where(state => !NonFlowTypes.Contains(TypeNameOf(state.Type))).ToList();
			var defaultFlows = CollectDefaultFlows(branches);
			var reservedIds = new HashSet<string>(states.Select(state => state.Id).Concat(branches.Select(branch => branch.Id)), StringComparer.Ordinal);
			var layout = Arrange(nodes, branches);

			var process = new XElement(ModelNamespace + "process",
				new XAttribute("id", Reserve(reservedIds, ProcessId)),
				new XAttribute("isExecutable", "false"),
				nodes.Select(node => CreateNode(node, defaultFlows.GetValueOrDefault(node.Id), timeUnit, currency)),
				branches.Select(CreateSequenceFlow));
			var plane = new XElement(DiNamespace + "BPMNPlane",
				new XAttribute("id", Reserve(reservedIds, PlaneId)),
				new XAttribute("bpmnElement", (string)process.Attribute("id")!),
				nodes.Select(node => CreateShape(node.Id, layout[node.Id], reservedIds)),
				branches.Where(branch => layout.ContainsKey(branch.Source) && layout.ContainsKey(branch.Target))
					.Select(branch => CreateEdge(branch, layout[branch.Source], layout[branch.Target], reservedIds)));
			var definitions = new XElement(ModelNamespace + "definitions",
				NamespaceDeclarations(),
				new XAttribute("id", Reserve(reservedIds, DefinitionsId)),
				new XAttribute("targetNamespace", TargetNamespace),
				process,
				new XElement(DiNamespace + "BPMNDiagram", new XAttribute("id", Reserve(reservedIds, DiagramId)), plane));

			return XmlDeclaration + Environment.NewLine + definitions;
		}

		private static void EnsureValidIds(IEnumerable<string> ids)
		{
			var seenIds = new HashSet<string>(StringComparer.Ordinal);
			foreach (var id in ids)
			{
				if (!IsNcName(id))
				{
					throw new InvalidRequestException($"The id '{id}' is not a valid BPMN identifier.");
				}

				if (!seenIds.Add(id))
				{
					throw new InvalidRequestException($"The id '{id}' is used more than once.");
				}
			}
		}

		private static bool IsNcName(string id)
		{
			try
			{
				XmlConvert.VerifyNCName(id);
				return true;
			}
			catch (XmlException)
			{
				return false;
			}
		}

		private static void EnsureValidTypes(IEnumerable<ProcessStateDTO> states)
		{
			var invalid = states.FirstOrDefault(state => !BpmnTypePattern().IsMatch(state.Type));
			if (invalid is not null)
			{
				throw new InvalidRequestException($"The type '{invalid.Type}' of state '{invalid.Id}' is not a BPMN element type such as 'bpmn:Task'.");
			}
		}

		private static Dictionary<string, string> CollectDefaultFlows(IEnumerable<ProcessBranchDTO> branches)
		{
			var defaultFlows = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var branch in branches.Where(IsDefaultBranch))
			{
				if (!defaultFlows.TryAdd(branch.Source, branch.Id))
				{
					throw new InvalidRequestException($"The state '{branch.Source}' has more than one '{DefaultBranchType}' (default) branch.");
				}
			}

			return defaultFlows;
		}

		private static bool IsDefaultBranch(ProcessBranchDTO branch) =>
			string.Equals(branch.Type?.Trim(), DefaultBranchType, StringComparison.OrdinalIgnoreCase);

		private static IReadOnlyDictionary<string, LayoutBounds> Arrange(IReadOnlyList<ProcessStateDTO> nodes, IEnumerable<ProcessBranchDTO> branches)
		{
			var nodeIds = nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
			var edges = branches
				.Where(branch => nodeIds.Contains(branch.Source) && nodeIds.Contains(branch.Target))
				.Select(branch => (branch.Source, branch.Target))
				.ToList();
			return ProcessJsonLayout.Arrange(nodes.Select(node => (node.Id, ElementNameOf(node.Type))).ToList(), edges);
		}

		private static XElement CreateNode(ProcessStateDTO state, string? defaultFlowId, ETimeUnit timeUnit, string currency)
		{
			var element = new XElement(ModelNamespace + ElementNameOf(state.Type), new XAttribute("id", state.Id));
			AddIfPresent(element, "name", state.Name);
			AddIfPresent(element, "default", defaultFlowId);
			AddIfPresent(element, ProjectOptimizerNamespace + "duration", MetricTextConverter.ToDuration(state.Time, timeUnit));
			AddIfPresent(element, ProjectOptimizerNamespace + "cost", MetricTextConverter.ToCost(state.Price, currency));
			AddIfPresent(element, FrontendNamespace + "time", state.Time?.Trim());
			AddIfPresent(element, FrontendNamespace + "price", state.Price?.Trim());
			return element;
		}

		private static XElement CreateSequenceFlow(ProcessBranchDTO branch)
		{
			var flow = new XElement(ModelNamespace + "sequenceFlow",
				new XAttribute("id", branch.Id),
				new XAttribute("sourceRef", branch.Source),
				new XAttribute("targetRef", branch.Target));
			AddIfPresent(flow, "name", branch.Name);
			if (!string.IsNullOrWhiteSpace(branch.Condition))
			{
				flow.Add(new XElement(ModelNamespace + "conditionExpression",
					new XAttribute(XmlSchemaInstanceNamespace + "type", FormalExpressionType),
					branch.Condition));
			}

			return flow;
		}

		private static XElement CreateShape(string id, LayoutBounds bounds, HashSet<string> reservedIds) =>
			new(DiNamespace + "BPMNShape",
				new XAttribute("id", Reserve(reservedIds, id + DiagramElementSuffix)),
				new XAttribute("bpmnElement", id),
				new XElement(DiagramCommonNamespace + "Bounds",
					new XAttribute("x", bounds.X),
					new XAttribute("y", bounds.Y),
					new XAttribute("width", bounds.Width),
					new XAttribute("height", bounds.Height)));

		private static XElement CreateEdge(ProcessBranchDTO branch, LayoutBounds source, LayoutBounds target, HashSet<string> reservedIds) =>
			new(DiNamespace + "BPMNEdge",
				new XAttribute("id", Reserve(reservedIds, branch.Id + DiagramElementSuffix)),
				new XAttribute("bpmnElement", branch.Id),
				WaypointsBetween(source, target).Select(point => new XElement(DiagramInterchangeNamespace + "waypoint",
					new XAttribute("x", point.X),
					new XAttribute("y", point.Y))));

		private static IEnumerable<(int X, int Y)> WaypointsBetween(LayoutBounds source, LayoutBounds target)
		{
			yield return (source.Right, source.CenterY);
			if (source.CenterY != target.CenterY)
			{
				var bendX = (source.Right + target.X) / 2;
				yield return (bendX, source.CenterY);
				yield return (bendX, target.CenterY);
			}

			yield return (target.X, target.CenterY);
		}

		private static IEnumerable<XAttribute> NamespaceDeclarations() =>
		[
			new(XNamespace.Xmlns + "bpmn", BpmnNamespaces.Model),
			new(XNamespace.Xmlns + "bpmndi", BpmnNamespaces.Di),
			new(XNamespace.Xmlns + "dc", BpmnNamespaces.DiagramCommon),
			new(XNamespace.Xmlns + "di", BpmnNamespaces.DiagramInterchange),
			new(XNamespace.Xmlns + "xsi", BpmnNamespaces.XmlSchemaInstance),
			new(XNamespace.Xmlns + "po", BpmnNamespaces.ProjectOptimizer),
			new(XNamespace.Xmlns + "optimizer", BpmnNamespaces.Frontend)
		];

		private static void AddIfPresent(XElement element, XName name, string? value)
		{
			if (!string.IsNullOrEmpty(value))
			{
				element.Add(new XAttribute(name, value));
			}
		}

		private static string Reserve(HashSet<string> reservedIds, string preferredId)
		{
			var id = preferredId;
			for (var suffix = 2; !reservedIds.Add(id); suffix++)
			{
				id = string.Create(CultureInfo.InvariantCulture, $"{preferredId}_{suffix}");
			}

			return id;
		}

		private static string TypeNameOf(string type) => type[(type.IndexOf(':') + 1)..];

		private static string ElementNameOf(string type)
		{
			var typeName = TypeNameOf(type);
			return char.ToLowerInvariant(typeName[0]) + typeName[1..];
		}

		[GeneratedRegex("^bpmn:[A-Z][A-Za-z]*$", RegexOptions.CultureInvariant)]
		private static partial Regex BpmnTypePattern();
	}
}
