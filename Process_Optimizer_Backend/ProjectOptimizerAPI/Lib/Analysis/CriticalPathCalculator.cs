using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed class CriticalPathCalculator : ICriticalPathCalculator
	{
		public CriticalPathResult Calculate(IReadOnlyList<FlowNode> orderedNodes, IReadOnlyList<SequenceFlowEdge> edges)
		{
			var network = NodeNetwork.Create(orderedNodes, edges);
			var earliestFinish = ComputeEarliestFinish(network);
			var makespan = ComputeMakespan(network, earliestFinish);
			var latestFinish = ComputeLatestFinish(network, makespan);
			var schedule = BuildSchedule(network, earliestFinish, latestFinish);
			var criticalPath = TraceCriticalPath(network, schedule);
			return new CriticalPathResult(schedule, criticalPath, DecimalNormalizer.Normalize(makespan));
		}

		private static decimal[] ComputeEarliestFinish(NodeNetwork network)
		{
			var earliestFinish = new decimal[network.Nodes.Count];
			for (var position = 0; position < network.Nodes.Count; position++)
			{
				var earliestStart = network.Predecessors[position].Select(predecessor => earliestFinish[predecessor]).DefaultIfEmpty(0m).Max();
				earliestFinish[position] = earliestStart + network.Nodes[position].Duration;
			}

			return earliestFinish;
		}

		private static decimal ComputeMakespan(NodeNetwork network, decimal[] earliestFinish) =>
			network.Sinks().Select(position => earliestFinish[position]).DefaultIfEmpty(0m).Max();

		private static decimal[] ComputeLatestFinish(NodeNetwork network, decimal makespan)
		{
			var latestFinish = new decimal[network.Nodes.Count];
			for (var position = network.Nodes.Count - 1; position >= 0; position--)
			{
				latestFinish[position] = network.Successors[position]
					.Select(successor => latestFinish[successor] - network.Nodes[successor].Duration)
					.DefaultIfEmpty(makespan)
					.Min();
			}

			return latestFinish;
		}

		private static List<ScheduledNode> BuildSchedule(NodeNetwork network, decimal[] earliestFinish, decimal[] latestFinish) =>
			network.Nodes
				.Select((node, position) => CreateScheduledNode(node, earliestFinish[position], latestFinish[position]))
				.ToList();

		private static ScheduledNode CreateScheduledNode(FlowNode node, decimal earliestFinish, decimal latestFinish)
		{
			var earliestStart = earliestFinish - node.Duration;
			var latestStart = latestFinish - node.Duration;
			var slack = latestStart - earliestStart;
			return new ScheduledNode(
				node,
				DecimalNormalizer.Normalize(earliestStart),
				DecimalNormalizer.Normalize(earliestFinish),
				DecimalNormalizer.Normalize(latestStart),
				DecimalNormalizer.Normalize(latestFinish),
				DecimalNormalizer.Normalize(slack),
				slack == 0m);
		}

		private static List<string> TraceCriticalPath(NodeNetwork network, IReadOnlyList<ScheduledNode> schedule)
		{
			var path = new List<string>();
			int? current = SelectLatestFinishing(network.Sinks(), schedule);
			while (current is int position)
			{
				path.Add(network.Nodes[position].BpmnId);
				current = SelectCriticalPredecessor(network.Predecessors[position], schedule, schedule[position].EarliestStart);
			}

			path.Reverse();
			return path;
		}

		private static int? SelectLatestFinishing(IEnumerable<int> candidates, IReadOnlyList<ScheduledNode> schedule) =>
			candidates
				.OrderByDescending(position => schedule[position].EarliestFinish)
				.ThenBy(position => position)
				.Select(position => (int?)position)
				.FirstOrDefault();

		private static int? SelectCriticalPredecessor(IEnumerable<int> predecessors, IReadOnlyList<ScheduledNode> schedule, decimal earliestStart) =>
			SelectLatestFinishing(
				predecessors.Where(position => schedule[position].IsCritical && schedule[position].EarliestFinish == earliestStart),
				schedule);
	}
}
