using System.Globalization;
using System.Security;
using System.Text;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerTests.Infrastructure
{
	public sealed class BpmnDiagramBuilder
	{
		public const string ProcessId = "Process_1";
		private const string ElementIndent = "    ";

		private readonly List<string> _nodes = [];
		private readonly List<string> _flows = [];

		public BpmnDiagramBuilder StartEvent(string id, string name = "") => AddNode($"<bpmn:startEvent{IdAndName(id, name)} />");

		public BpmnDiagramBuilder EndEvent(string id, string name = "") => AddNode($"<bpmn:endEvent{IdAndName(id, name)} />");

		public BpmnDiagramBuilder Task(string id, string name, decimal? duration, decimal? cost, bool? parallelizable = null)
		{
			var attributes = IdAndName(id, name)
				+ OptionalAttribute("po:duration", FormatDecimal(duration))
				+ OptionalAttribute("po:cost", FormatDecimal(cost))
				+ OptionalAttribute("po:parallelizable", parallelizable?.ToString().ToLowerInvariant());
			return AddNode($"<bpmn:task{attributes} />");
		}

		public BpmnDiagramBuilder Gateway(string id, EFlowNodeKind kind, string? defaultFlowId = null) =>
			AddNode($"<bpmn:{GatewayLocalName(kind)}{IdAndName(id, string.Empty)}{OptionalAttribute("default", defaultFlowId)} />");

		public BpmnDiagramBuilder Node(string localName, string id) => AddNode($"<bpmn:{localName}{IdAndName(id, string.Empty)} />");

		public BpmnDiagramBuilder Flow(string id, string sourceId, string targetId, string? condition = null)
		{
			var attributes = $"{Attribute("id", id)}{Attribute("sourceRef", sourceId)}{Attribute("targetRef", targetId)}";
			_flows.Add(condition is null
				? $"<bpmn:sequenceFlow{attributes} />"
				: $"<bpmn:sequenceFlow{attributes}><bpmn:conditionExpression>{SecurityElement.Escape(condition)}</bpmn:conditionExpression></bpmn:sequenceFlow>");
			return this;
		}

		public BpmnDiagramBuilder Raw(string xmlInsideProcess) => AddNode(xmlInsideProcess);

		public string Build()
		{
			var xml = new StringBuilder();
			xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
			xml.Append($"<bpmn:definitions xmlns:bpmn=\"{BpmnNamespaces.Model}\" xmlns:po=\"{BpmnNamespaces.ProjectOptimizer}\" id=\"Definitions_1\" targetNamespace=\"http://bpmn.io/schema/bpmn\">\n");
			xml.Append($"  <bpmn:process id=\"{ProcessId}\" isExecutable=\"false\">\n");
			foreach (var element in _nodes.Concat(_flows))
			{
				xml.Append(ElementIndent).Append(element).Append('\n');
			}

			xml.Append("  </bpmn:process>\n");
			xml.Append("</bpmn:definitions>");
			return xml.ToString();
		}

		public static string SequentialExclusiveChoices(int choiceCount)
		{
			var builder = new BpmnDiagramBuilder().StartEvent("Start");
			var previousId = "Start";
			for (var choice = 1; choice <= choiceCount; choice++)
			{
				builder.AddExclusiveChoice(choice, previousId);
				previousId = $"Join{choice}";
			}

			return builder.EndEvent("End").Flow("FlowEnd", previousId, "End").Build();
		}

		public static string Chain(int taskCount)
		{
			var builder = new BpmnDiagramBuilder().StartEvent("Start");
			var previousId = "Start";
			for (var task = 1; task <= taskCount; task++)
			{
				var taskId = $"Task{task}";
				builder.Task(taskId, $"Task {task}", 1, 100).Flow($"Flow{task}", previousId, taskId);
				previousId = taskId;
			}

			return builder.EndEvent("End").Flow("FlowEnd", previousId, "End").Build();
		}

		private void AddExclusiveChoice(int choice, string previousId)
		{
			var splitId = $"Split{choice}";
			var joinId = $"Join{choice}";
			Gateway(splitId, EFlowNodeKind.ExclusiveGateway)
				.Gateway(joinId, EFlowNodeKind.ExclusiveGateway)
				.Flow($"FlowIn{choice}", previousId, splitId);
			foreach (var branch in new[] { "A", "B" })
			{
				var taskId = $"Task{choice}{branch}";
				Task(taskId, $"Task {choice}{branch}", choice, 100)
					.Flow($"FlowSplit{choice}{branch}", splitId, taskId)
					.Flow($"FlowJoin{choice}{branch}", taskId, joinId);
			}
		}

		private BpmnDiagramBuilder AddNode(string element)
		{
			_nodes.Add(element);
			return this;
		}

		private static string GatewayLocalName(EFlowNodeKind kind) => kind switch
		{
			EFlowNodeKind.ExclusiveGateway => "exclusiveGateway",
			EFlowNodeKind.ParallelGateway => "parallelGateway",
			EFlowNodeKind.InclusiveGateway => "inclusiveGateway",
			EFlowNodeKind.EventBasedGateway => "eventBasedGateway",
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind is not a gateway.")
		};

		private static string IdAndName(string id, string name) => Attribute("id", id) + (name.Length == 0 ? string.Empty : Attribute("name", name));

		private static string OptionalAttribute(string name, string? value) => value is null ? string.Empty : Attribute(name, value);

		private static string Attribute(string name, string value) => $" {name}=\"{SecurityElement.Escape(value)}\"";

		private static string? FormatDecimal(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);
	}
}
