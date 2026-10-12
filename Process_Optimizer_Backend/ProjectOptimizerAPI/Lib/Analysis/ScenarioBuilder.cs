using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed class ScenarioBuilder(ICriticalPathCalculator criticalPathCalculator) : IScenarioBuilder
	{
		private const string CyclicScenarioMessage = "The scenario graph contains a cycle.";

		public ScenarioComputation Build(ProcessGraph graph, ScenarioSelection selection)
		{
			var allowedEdges = RemoveUnchosenEdges(graph.Edges, selection.Choices);
			var reachedIds = CollectReachableIds(graph, allowedEdges);
			var scenarioEdges = allowedEdges.Where(edge => reachedIds.Contains(edge.SourceId) && reachedIds.Contains(edge.TargetId)).ToList();
			var orderedNodes = SortTopologically(graph.Nodes.Where(node => reachedIds.Contains(node.BpmnId)).ToList(), scenarioEdges);
			return CreateComputation(selection, orderedNodes, scenarioEdges);
		}

		public ScenarioDTO ToDto(ScenarioComputation computation, ProjectContext context, long count) =>
			new()
			{
				ProjectId = context.ProjectId,
				OrderingVersion = AnalysisLimits.OrderingVersion,
				Index = computation.Selection.Index,
				Count = count,
				Key = computation.Selection.Key,
				Choices = ToChoiceDtos(computation.Selection.Choices),
				ElementIds = computation.ElementIds.ToList(),
				SequenceFlowIds = computation.SequenceFlowIds.ToList(),
				Activities = computation.Activities.Select(ToActivityDto).ToList(),
				CriticalPath = computation.CriticalPath.ToList(),
				PathCount = computation.PathCount,
				TotalDuration = computation.TotalDuration,
				SequentialDuration = computation.SequentialDuration,
				TotalCost = computation.TotalCost,
				HasExclusiveActivities = computation.HasExclusiveActivities,
				HasMissingEstimates = computation.HasMissingEstimates,
				TimeUnit = context.TimeUnit,
				Currency = context.Currency
			};

		private ScenarioComputation CreateComputation(ScenarioSelection selection, IReadOnlyList<FlowNode> orderedNodes, IReadOnlyList<SequenceFlowEdge> scenarioEdges)
		{
			var criticalPath = criticalPathCalculator.Calculate(orderedNodes, scenarioEdges);
			var network = NodeNetwork.Create(orderedNodes, scenarioEdges);
			return new ScenarioComputation(
				selection,
				orderedNodes.Select(node => node.BpmnId).ToList(),
				OrderSequenceFlowIds(network, scenarioEdges),
				criticalPath.Schedule.Where(scheduled => IsReportedActivity(scheduled.Node)).ToList(),
				criticalPath.CriticalPath,
				CountPaths(network),
				criticalPath.Makespan,
				DecimalNormalizer.Normalize(orderedNodes.Sum(node => node.Duration)),
				DecimalNormalizer.Normalize(orderedNodes.Sum(node => node.Cost)),
				orderedNodes.Any(node => !node.Parallelizable),
				orderedNodes.Any(node => node.IsMissingEstimate));
		}

		private static List<SequenceFlowEdge> RemoveUnchosenEdges(IReadOnlyList<SequenceFlowEdge> edges, IReadOnlyList<ScenarioChoice> choices)
		{
			var chosenTargetsByGateway = choices.ToDictionary(
				choice => choice.GatewayId,
				choice => choice.ChosenTargetIds.ToHashSet(StringComparer.Ordinal),
				StringComparer.Ordinal);
			return edges
				.Where(edge => !chosenTargetsByGateway.TryGetValue(edge.SourceId, out var chosenTargets) || chosenTargets.Contains(edge.TargetId))
				.ToList();
		}

		private static HashSet<string> CollectReachableIds(ProcessGraph graph, IReadOnlyList<SequenceFlowEdge> edges)
		{
			var successors = edges.ToLookup(edge => edge.SourceId, edge => edge.TargetId, StringComparer.Ordinal);
			var startIds = graph.Nodes.Where(node => node.Kind == EFlowNodeKind.StartEvent).Select(node => node.BpmnId);
			return GraphTraversal.CollectReachable(startIds, successors);
		}

		private static IReadOnlyList<FlowNode> SortTopologically(IReadOnlyCollection<FlowNode> nodes, IReadOnlyCollection<SequenceFlowEdge> edges) =>
			TopologicalOrder.Sort(nodes, edges) ?? throw new InvalidOperationException(CyclicScenarioMessage);

		private static List<string> OrderSequenceFlowIds(NodeNetwork network, IReadOnlyList<SequenceFlowEdge> edges) =>
			edges
				.OrderBy(edge => network.Positions[edge.SourceId])
				.ThenBy(edge => network.Positions[edge.TargetId])
				.ThenBy(edge => edge.BpmnId, StringComparer.Ordinal)
				.Select(edge => edge.BpmnId)
				.ToList();

		private static long CountPaths(NodeNetwork network)
		{
			var pathsTo = network.Predecessors.Select(predecessors => predecessors.Count > 0 ? 0L : 1L).ToArray();
			foreach (var link in network.Links.OrderBy(link => link.Source))
			{
				pathsTo[link.Target] = AddSaturating(pathsTo[link.Target], pathsTo[link.Source]);
			}

			return network.Sinks().Aggregate(0L, (total, position) => AddSaturating(total, pathsTo[position]));
		}

		private static long AddSaturating(long left, long right) => Math.Min(AnalysisLimits.MaxPathCount, left + right);

		private static bool IsReportedActivity(FlowNode node) => node.IsActivity || node.Duration > 0m || node.Cost > 0m;

		private static List<ScenarioChoiceDTO> ToChoiceDtos(IReadOnlyList<ScenarioChoice> choices) =>
			choices
				.Select(choice => new ScenarioChoiceDTO
				{
					GatewayId = choice.GatewayId,
					GatewayKind = choice.GatewayKind,
					IsControllable = choice.IsControllable,
					ChosenTargetIds = choice.ChosenTargetIds.ToList()
				})
				.ToList();

		private static ScenarioActivityDTO ToActivityDto(ScheduledNode scheduled) =>
			new()
			{
				BpmnId = scheduled.Node.BpmnId,
				Name = scheduled.Node.Name,
				Kind = scheduled.Node.Kind,
				Duration = DecimalNormalizer.Normalize(scheduled.Node.Duration),
				Cost = DecimalNormalizer.Normalize(scheduled.Node.Cost),
				Parallelizable = scheduled.Node.Parallelizable,
				EarliestStart = scheduled.EarliestStart,
				EarliestFinish = scheduled.EarliestFinish,
				LatestStart = scheduled.LatestStart,
				LatestFinish = scheduled.LatestFinish,
				Slack = scheduled.Slack,
				IsCritical = scheduled.IsCritical
			};
	}
}
