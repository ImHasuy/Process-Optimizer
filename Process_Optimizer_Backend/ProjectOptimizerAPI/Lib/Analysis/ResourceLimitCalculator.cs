using System.Numerics;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerLib.DTO;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed class ResourceLimitCalculator : IResourceLimitCalculator
	{
		public const string MissingEstimatesNote = "Some activities have no duration or cost; the limits may be too low.";
		public const string TooManyScenariosNote = "The diagram has too many scenarios to index; scenario references are omitted.";

		private static Metric FirstScenario => new(_ => 0m, IsMaximum: false, IsParallelSum: true);

		public ResourceLimitsDTO Calculate(ProcessGraph graph, StructureFragment root, EResourceLimitBasis basis, ProjectContext context)
		{
			var leaves = new LeafValues(graph, basis);
			var limits = EvaluateLimits(root, leaves);
			var limitContext = new LimitContext(
				context,
				basis,
				root.Count,
				graph.Nodes.Any(node => !node.Parallelizable),
				graph.Nodes.Any(node => node.IsMissingEstimate),
				EvaluateJointWorstCase(root, leaves).Duration == limits.MaxCriticalPath.Value);
			return ToDto(limits, limitContext);
		}

		private static Limits EvaluateLimits(StructureFragment root, LeafValues leaves) => new(
			Evaluate(root, new Metric(leaves.Cost, IsMaximum: true, IsParallelSum: true)),
			Evaluate(root, new Metric(leaves.Cost, IsMaximum: false, IsParallelSum: true)),
			Evaluate(root, new Metric(leaves.Duration, IsMaximum: true, IsParallelSum: false)),
			Evaluate(root, new Metric(leaves.Duration, IsMaximum: false, IsParallelSum: false)),
			Evaluate(root, new Metric(leaves.Duration, IsMaximum: true, IsParallelSum: true)),
			Evaluate(root, new Metric(leaves.ExclusiveDuration, IsMaximum: true, IsParallelSum: true)));

		private static ResourceLimitsDTO ToDto(Limits limits, LimitContext limitContext) => new()
		{
			ProjectId = limitContext.Context.ProjectId,
			Basis = limitContext.Basis,
			TimeUnit = limitContext.Context.TimeUnit,
			Currency = limitContext.Context.Currency,
			ScenarioCount = limitContext.IsIndexable ? (long)limitContext.ScenarioCount : null,
			MaxCost = DecimalNormalizer.Normalize(limits.MaxCost.Value),
			MaxCostScenario = ToReference(limits.MaxCost, limitContext.IsIndexable),
			MinCost = DecimalNormalizer.Normalize(limits.MinCost.Value),
			MinCostScenario = ToReference(limits.MinCost, limitContext.IsIndexable),
			MaxCriticalPathDuration = DecimalNormalizer.Normalize(limits.MaxCriticalPath.Value),
			MaxCriticalPathScenario = ToReference(limits.MaxCriticalPath, limitContext.IsIndexable),
			MinCriticalPathDuration = DecimalNormalizer.Normalize(limits.MinCriticalPath.Value),
			MinCriticalPathScenario = ToReference(limits.MinCriticalPath, limitContext.IsIndexable),
			MaxExclusiveDuration = DecimalNormalizer.Normalize(limits.MaxExclusive.Value),
			MaxDurationBound = DecimalNormalizer.Normalize(limits.MaxCriticalPath.Value + limits.MaxExclusive.Value),
			MaxSequentialDuration = DecimalNormalizer.Normalize(limits.MaxSequential.Value),
			MaxSequentialScenario = ToReference(limits.MaxSequential, limitContext.IsIndexable),
			IsJointWorstCase = limitContext.IsJointWorstCase,
			HasExclusiveActivities = limitContext.HasExclusiveActivities,
			HasMissingEstimates = limitContext.HasMissingEstimates,
			Notes = CreateNotes(limitContext)
		};

		private static List<string> CreateNotes(LimitContext limitContext)
		{
			var notes = new List<string>();
			if (limitContext.HasMissingEstimates)
			{
				notes.Add(MissingEstimatesNote);
			}
			if (!limitContext.IsIndexable)
			{
				notes.Add(TooManyScenariosNote);
			}
			return notes;
		}

		private static ScenarioRefDTO? ToReference(LimitSelection selection, bool isIndexable) => isIndexable
			? new ScenarioRefDTO { Index = (long)selection.Index, Key = ScenarioKeyFormatter.Format(selection.Choices) }
			: null;

		private static LimitSelection Evaluate(StructureFragment fragment, Metric metric)
		{
			if (!metric.Selections.TryGetValue(fragment, out var selection))
			{
				selection = EvaluateUncached(fragment, metric);
				metric.Selections[fragment] = selection;
			}

			return selection;
		}

		private static LimitSelection EvaluateUncached(StructureFragment fragment, Metric metric) => fragment.Kind switch
		{
			EFragmentKind.Leaf => new LimitSelection(metric.LeafValue(LeafNodeId(fragment)), BigInteger.Zero, []),
			EFragmentKind.Sequence => EvaluateSequence(fragment, metric),
			EFragmentKind.Parallel => EvaluateParallel(fragment, metric),
			EFragmentKind.Exclusive => EvaluateExclusive(fragment, metric),
			_ => EvaluateInclusive(fragment, metric)
		};

		private static string LeafNodeId(StructureFragment fragment) => fragment.NodeId ?? fragment.EntryNodeId;

		private static List<LimitSelection> EvaluateAll(IReadOnlyList<StructureFragment> fragments, Metric metric) =>
			fragments.Select(fragment => Evaluate(fragment, metric)).ToList();

		private static LimitSelection EvaluateSequence(StructureFragment fragment, Metric metric)
		{
			var selections = EvaluateAll(fragment.Children, metric);
			return Compose(fragment.Children, selections, selections.Sum(selection => selection.Value));
		}

		private static LimitSelection EvaluateParallel(StructureFragment fragment, Metric metric)
		{
			var selections = EvaluateAll(fragment.Children, metric);
			if (metric.IsParallelSum)
			{
				return Compose(fragment.Children, selections, selections.Sum(selection => selection.Value));
			}
			return metric.IsMaximum
				? SelectLongestBranch(fragment.Children, selections)
				: SelectLowestParallelWithin(fragment.Children, metric, selections.Max(selection => selection.Value))!;
		}

		private static LimitSelection? SelectLowestWithin(StructureFragment fragment, Metric metric, decimal bound) =>
			Evaluate(fragment, metric).Value > bound ? null : SelectLowestFeasible(fragment, metric, bound);

		private static LimitSelection? SelectLowestFeasible(StructureFragment fragment, Metric metric, decimal bound) => fragment.Kind switch
		{
			EFragmentKind.Leaf => SelectLeafWithin(metric.LeafValue(LeafNodeId(fragment)), bound),
			EFragmentKind.Sequence => SelectLowestSequenceWithin(fragment.Children, metric, bound),
			EFragmentKind.Parallel => SelectLowestParallelWithin(fragment.Children, metric, bound),
			_ => SelectLowestChoiceWithin(fragment, metric, bound)
		};

		private static LimitSelection? SelectLeafWithin(decimal value, decimal bound) =>
			value <= bound ? new LimitSelection(value, BigInteger.Zero, []) : null;

		private static LimitSelection? SelectLowestSequenceWithin(IReadOnlyList<StructureFragment> parts, Metric metric, decimal bound)
		{
			var minima = parts.Select(part => Evaluate(part, metric).Value).ToList();
			var selections = new List<LimitSelection>();
			var remaining = bound;
			for (var position = 0; position < parts.Count; position++)
			{
				var selection = SelectLowestWithin(parts[position], metric, remaining - minima.Skip(position + 1).Sum());
				if (selection is null)
				{
					return null;
				}

				selections.Add(selection);
				remaining -= selection.Value;
			}

			return Compose(parts, selections, selections.Sum(selection => selection.Value));
		}

		private static LimitSelection? SelectLowestParallelWithin(IReadOnlyList<StructureFragment> branches, Metric metric, decimal bound)
		{
			var selections = new List<LimitSelection>();
			foreach (var branch in branches)
			{
				if (SelectLowestWithin(branch, metric, bound) is not { } selection)
				{
					return null;
				}

				selections.Add(selection);
			}

			return Compose(branches, selections, selections.Max(selection => selection.Value));
		}

		private static LimitSelection? SelectLowestChoiceWithin(StructureFragment fragment, Metric metric, decimal bound)
		{
			var offset = BigInteger.Zero;
			foreach (var alternative in FragmentChoices.EnumerateAlternatives(fragment))
			{
				var branches = FragmentChoices.SelectBranches(fragment, alternative);
				if (SelectLowestParallelWithin(branches, metric, bound) is { } inner)
				{
					return PrefixChoice(fragment, branches, offset, inner);
				}

				offset += FragmentChoices.Product(branches);
			}

			return null;
		}

		private static LimitSelection SelectLongestBranch(IReadOnlyList<StructureFragment> branches, IReadOnlyList<LimitSelection> selections)
		{
			var longest = selections.Max(selection => selection.Value);
			var firstSelections = EvaluateAll(branches, FirstScenario);
			return Enumerable.Range(0, branches.Count)
				.Where(position => selections[position].Value == longest)
				.Select(position => Compose(branches, ReplaceAt(firstSelections, position, selections[position]), longest))
				.MinBy(selection => selection.Index)!;
		}

		private static List<LimitSelection> ReplaceAt(IReadOnlyList<LimitSelection> selections, int position, LimitSelection replacement) =>
			selections.Select((selection, current) => current == position ? replacement : selection).ToList();

		private static LimitSelection EvaluateExclusive(StructureFragment fragment, Metric metric)
		{
			var selections = EvaluateAll(fragment.Children, metric);
			return ChooseBranches(fragment, selections, [SelectBestBranch(selections, metric)]);
		}

		private static LimitSelection EvaluateInclusive(StructureFragment fragment, Metric metric)
		{
			var selections = EvaluateAll(fragment.Children, metric);
			IReadOnlyList<int> positions = metric.IsMaximum && metric.IsParallelSum
				? SelectPositiveBranches(selections)
				: [SelectBestBranch(selections, metric)];
			return ChooseBranches(fragment, selections, positions);
		}

		private static int SelectBestBranch(List<LimitSelection> selections, Metric metric)
		{
			var best = metric.IsMaximum
				? selections.Max(selection => selection.Value)
				: selections.Min(selection => selection.Value);
			return selections.FindIndex(selection => selection.Value == best);
		}

		private static List<int> SelectPositiveBranches(IReadOnlyList<LimitSelection> selections)
		{
			var positive = Enumerable.Range(0, selections.Count).Where(position => selections[position].Value > 0m).ToList();
			return positive.Count > 0 ? positive : [0];
		}

		private static LimitSelection ChooseBranches(StructureFragment fragment, IReadOnlyList<LimitSelection> selections, IReadOnlyList<int> positions)
		{
			var branches = FragmentChoices.SelectBranches(fragment, positions);
			var chosen = positions.Select(position => selections[position]).ToList();
			var inner = Compose(branches, chosen, chosen.Sum(selection => selection.Value));
			return PrefixChoice(fragment, branches, FragmentChoices.OffsetOf(fragment, positions), inner);
		}

		private static LimitSelection PrefixChoice(StructureFragment fragment, IReadOnlyList<StructureFragment> branches, BigInteger offset, LimitSelection inner) =>
			new(inner.Value, offset + inner.Index, [FragmentChoices.CreateChoice(fragment, branches), .. inner.Choices]);

		private static LimitSelection Compose(IReadOnlyList<StructureFragment> parts, IReadOnlyList<LimitSelection> selections, decimal value)
		{
			var index = BigInteger.Zero;
			for (var position = 0; position < parts.Count; position++)
			{
				index = index * parts[position].Count + selections[position].Index;
			}
			return new LimitSelection(value, index, selections.SelectMany(selection => selection.Choices).ToList());
		}

		private static JointValue EvaluateJointWorstCase(StructureFragment fragment, LeafValues leaves)
		{
			if (fragment.Kind == EFragmentKind.Leaf)
			{
				return new JointValue(leaves.Cost(LeafNodeId(fragment)), leaves.Duration(LeafNodeId(fragment)));
			}

			var children = fragment.Children.Select(child => EvaluateJointWorstCase(child, leaves)).ToList();
			return fragment.Kind switch
			{
				EFragmentKind.Sequence => new JointValue(children.Sum(child => child.Cost), children.Sum(child => child.Duration)),
				EFragmentKind.Exclusive => children.OrderByDescending(child => child.Cost).ThenByDescending(child => child.Duration).First(),
				_ => new JointValue(children.Sum(child => child.Cost), children.Max(child => child.Duration))
			};
		}

		private sealed record Metric(Func<string, decimal> LeafValue, bool IsMaximum, bool IsParallelSum)
		{
			public Dictionary<StructureFragment, LimitSelection> Selections { get; } = new(ReferenceEqualityComparer.Instance);
		}

		private sealed record LimitSelection(decimal Value, BigInteger Index, IReadOnlyList<ScenarioChoice> Choices);

		private sealed record JointValue(decimal Cost, decimal Duration);

		private sealed record LimitContext(
			ProjectContext Context,
			EResourceLimitBasis Basis,
			BigInteger ScenarioCount,
			bool HasExclusiveActivities,
			bool HasMissingEstimates,
			bool IsJointWorstCase)
		{
			public bool IsIndexable => AnalysisLimits.IsIndexable(ScenarioCount);
		}

		private sealed record Limits(
			LimitSelection MaxCost,
			LimitSelection MinCost,
			LimitSelection MaxCriticalPath,
			LimitSelection MinCriticalPath,
			LimitSelection MaxSequential,
			LimitSelection MaxExclusive);

		private sealed class LeafValues(ProcessGraph graph, EResourceLimitBasis basis)
		{
			public decimal Cost(string nodeId) => graph.NodesById[nodeId].Cost;

			public decimal Duration(string nodeId) => DurationOf(graph.NodesById[nodeId]);

			public decimal ExclusiveDuration(string nodeId)
			{
				var node = graph.NodesById[nodeId];
				return node.Parallelizable ? 0m : DurationOf(node);
			}

			private decimal DurationOf(FlowNode node) =>
				basis == EResourceLimitBasis.Pessimistic && node.Estimate is not null ? node.Estimate.Pessimistic : node.Duration;
		}
	}
}
