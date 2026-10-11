using System.Globalization;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed class DiagramValidator(IProcessStructureAnalyzer processStructureAnalyzer) : IDiagramValidator
	{
		private const string StartEventMissingMessage = "The process has no start event.";
		private const string EndEventMissingMessage = "The process has no end event.";
		private const string UnreachableMessage = "The node is not reachable from a start event.";
		private const string DeadEndMessage = "The node does not reach an end event.";
		private const string CycleMessage = "The node is part of a cycle.";
		private const string MissingEstimateMessage = "The activity has no po:duration or no po:cost; 0 is assumed.";
		private const string MixedGatewayMessage = "The gateway has several incoming and several outgoing sequence flows; model it as two gateways.";
		private const string ImplicitSplitOrJoinMessage = "The node has several incoming or outgoing sequence flows and is interpreted as a parallel join or split; use an explicit parallel gateway.";
		private const string IgnoredConditionMessage = "The condition is ignored because the sequence flow does not leave an exclusive or inclusive split.";
		private const string DuplicateNameMessageFormat = "Several nodes share the name '{0}'.";
		private static readonly string TooManyInclusiveBranchesMessage = string.Create(CultureInfo.InvariantCulture, $"An inclusive split may have at most {AnalysisLimits.MaxInclusiveBranches} outgoing branches.");

		public DiagramValidationResult Validate(ProcessGraph graph)
		{
			var degrees = NodeDegrees.From(graph);
			var issues = graph.ParseIssues
				.Concat(CheckStartEventExists(graph))
				.Concat(CheckEndEventExists(graph))
				.Concat(CheckReachableFromStart(graph))
				.Concat(CheckReachesEnd(graph))
				.Concat(CheckAcyclic(graph))
				.Concat(CheckActivityEstimates(graph))
				.Concat(CheckMixedGateways(graph, degrees))
				.Concat(CheckImplicitSplitsAndJoins(graph, degrees))
				.Concat(CheckIgnoredConditions(graph))
				.Concat(CheckDuplicateNames(graph))
				.Concat(CheckInclusiveBranchLimit(graph, degrees))
				.ToList();
			var structure = AnalyzeStructure(graph, issues);
			return CreateResult(issues, structure);
		}

		private static IEnumerable<ValidationIssue> CheckStartEventExists(ProcessGraph graph) =>
			HasNodeOfKind(graph, EFlowNodeKind.StartEvent)
				? []
				: [Error(DiagramIssueCodes.StartEventMissing, null, StartEventMissingMessage)];

		private static IEnumerable<ValidationIssue> CheckEndEventExists(ProcessGraph graph) =>
			HasNodeOfKind(graph, EFlowNodeKind.EndEvent)
				? []
				: [Error(DiagramIssueCodes.EndEventMissing, null, EndEventMissingMessage)];

		private static IEnumerable<ValidationIssue> CheckReachableFromStart(ProcessGraph graph)
		{
			var successors = graph.Edges.ToLookup(edge => edge.SourceId, edge => edge.TargetId, StringComparer.Ordinal);
			return FindUnreachedNodes(graph, EFlowNodeKind.StartEvent, successors)
				.Select(node => Error(DiagramIssueCodes.Unreachable, node.BpmnId, UnreachableMessage));
		}

		private static IEnumerable<ValidationIssue> CheckReachesEnd(ProcessGraph graph)
		{
			var predecessors = graph.Edges.ToLookup(edge => edge.TargetId, edge => edge.SourceId, StringComparer.Ordinal);
			return FindUnreachedNodes(graph, EFlowNodeKind.EndEvent, predecessors)
				.Select(node => Error(DiagramIssueCodes.DeadEnd, node.BpmnId, DeadEndMessage));
		}

		private static IEnumerable<ValidationIssue> CheckAcyclic(ProcessGraph graph)
		{
			var orderedIds = TopologicalOrder.SortAcyclicPart(graph.Nodes, graph.Edges)
				.Select(node => node.BpmnId)
				.ToHashSet(StringComparer.Ordinal);
			return graph.Nodes
				.Where(node => !orderedIds.Contains(node.BpmnId))
				.Select(node => Error(DiagramIssueCodes.Cycle, node.BpmnId, CycleMessage));
		}

		private static IEnumerable<ValidationIssue> CheckActivityEstimates(ProcessGraph graph) =>
			graph.Nodes
				.Where(node => node.IsMissingEstimate)
				.Select(node => Warning(DiagramIssueCodes.MissingEstimate, node.BpmnId, MissingEstimateMessage));

		private static IEnumerable<ValidationIssue> CheckMixedGateways(ProcessGraph graph, NodeDegrees degrees) =>
			graph.Nodes
				.Where(node => node.IsGateway && degrees.IsJoining(node) && degrees.IsSplitting(node))
				.Select(node => Error(DiagramIssueCodes.MixedGateway, node.BpmnId, MixedGatewayMessage));

		private static IEnumerable<ValidationIssue> CheckImplicitSplitsAndJoins(ProcessGraph graph, NodeDegrees degrees) =>
			graph.Nodes
				.Where(node => !node.IsGateway && (degrees.IsJoining(node) || degrees.IsSplitting(node)))
				.Select(node => Warning(DiagramIssueCodes.ImplicitSplitOrJoin, node.BpmnId, ImplicitSplitOrJoinMessage));

		private static IEnumerable<ValidationIssue> CheckIgnoredConditions(ProcessGraph graph) =>
			graph.Edges
				.Where(edge => edge.ConditionExpression is not null && !LeavesConditionalSplit(graph, edge))
				.Select(edge => Warning(DiagramIssueCodes.IgnoredCondition, edge.BpmnId, IgnoredConditionMessage));

		private static IEnumerable<ValidationIssue> CheckDuplicateNames(ProcessGraph graph) =>
			graph.Nodes
				.Where(node => !string.IsNullOrWhiteSpace(node.Name))
				.GroupBy(node => node.Name, StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
				.SelectMany(group => group.Select(node => Warning(DiagramIssueCodes.DuplicateName, node.BpmnId, FormatDuplicateNameMessage(group.Key))));

		private static IEnumerable<ValidationIssue> CheckInclusiveBranchLimit(ProcessGraph graph, NodeDegrees degrees) =>
			graph.Nodes
				.Where(node => IsSplitOfKind(node, EFlowNodeKind.InclusiveGateway) && degrees.OutgoingOf(node) > AnalysisLimits.MaxInclusiveBranches)
				.Select(node => Error(DiagramIssueCodes.TooManyInclusiveBranches, node.BpmnId, TooManyInclusiveBranchesMessage));

		private StructureFragment? AnalyzeStructure(ProcessGraph graph, List<ValidationIssue> issues)
		{
			if (HasError(issues))
			{
				return null;
			}
			var analysis = processStructureAnalyzer.Analyze(graph);
			issues.AddRange(analysis.Issues);
			return analysis.Root;
		}

		private static DiagramValidationResult CreateResult(IEnumerable<ValidationIssue> issues, StructureFragment? structure)
		{
			var sortedIssues = issues
				.OrderBy(issue => issue.Code, StringComparer.Ordinal)
				.ThenBy(issue => issue.BpmnElementId, StringComparer.Ordinal)
				.ThenBy(issue => issue.Message, StringComparer.Ordinal)
				.ToList();
			var isOptimizable = !HasError(sortedIssues);
			return new DiagramValidationResult(sortedIssues, isOptimizable, isOptimizable ? structure : null);
		}

		private static IEnumerable<FlowNode> FindUnreachedNodes(ProcessGraph graph, EFlowNodeKind seedKind, ILookup<string, string> neighbours)
		{
			var seedIds = graph.Nodes.Where(node => node.Kind == seedKind).Select(node => node.BpmnId).ToList();
			if (seedIds.Count == 0)
			{
				return [];
			}
			var reachedIds = GraphTraversal.CollectReachable(seedIds, neighbours);
			return graph.Nodes.Where(node => !reachedIds.Contains(node.BpmnId));
		}

		private static bool LeavesConditionalSplit(ProcessGraph graph, SequenceFlowEdge edge) =>
			graph.NodesById.TryGetValue(edge.SourceId, out var source)
			&& (IsSplitOfKind(source, EFlowNodeKind.ExclusiveGateway) || IsSplitOfKind(source, EFlowNodeKind.InclusiveGateway));

		private static bool IsSplitOfKind(FlowNode node, EFlowNodeKind kind) =>
			node.Kind == kind && node.GatewayDirection == EGatewayDirection.Split;

		private static bool HasNodeOfKind(ProcessGraph graph, EFlowNodeKind kind) =>
			graph.Nodes.Any(node => node.Kind == kind);

		private static bool HasError(IEnumerable<ValidationIssue> issues) =>
			issues.Any(issue => issue.Severity == EIssueSeverity.Error);

		private static string FormatDuplicateNameMessage(string name) =>
			string.Format(CultureInfo.InvariantCulture, DuplicateNameMessageFormat, name);

		private static ValidationIssue Error(string code, string? bpmnElementId, string message) =>
			new(code, EIssueSeverity.Error, bpmnElementId, message);

		private static ValidationIssue Warning(string code, string? bpmnElementId, string message) =>
			new(code, EIssueSeverity.Warning, bpmnElementId, message);

		private sealed class NodeDegrees(IReadOnlyDictionary<string, int> incomingCounts, IReadOnlyDictionary<string, int> outgoingCounts)
		{
			public static NodeDegrees From(ProcessGraph graph) =>
				new(CountBy(graph.Edges, edge => edge.TargetId), CountBy(graph.Edges, edge => edge.SourceId));

			public int OutgoingOf(FlowNode node) => outgoingCounts.GetValueOrDefault(node.BpmnId);

			public bool IsJoining(FlowNode node) => incomingCounts.GetValueOrDefault(node.BpmnId) >= AnalysisLimits.MinBranchingFlows;

			public bool IsSplitting(FlowNode node) => OutgoingOf(node) >= AnalysisLimits.MinBranchingFlows;

			private static Dictionary<string, int> CountBy(IEnumerable<SequenceFlowEdge> edges, Func<SequenceFlowEdge, string> nodeIdSelector) =>
				edges
					.GroupBy(nodeIdSelector, StringComparer.Ordinal)
					.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
		}
	}
}
