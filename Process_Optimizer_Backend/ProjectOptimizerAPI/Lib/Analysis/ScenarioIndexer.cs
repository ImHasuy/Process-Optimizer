using System.Numerics;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerAPI.Lib.Interface;
using static ProjectOptimizerAPI.Lib.Analysis.FragmentChoices;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public sealed class ScenarioIndexer : IScenarioIndexer
	{
		public ScenarioSelection Unrank(StructureFragment root, long index)
		{
			var choices = new List<ScenarioChoice>();
			Decode(root, index, choices);
			return new ScenarioSelection(index, ScenarioKeyFormatter.Format(choices), choices);
		}

		public long? Rank(StructureFragment root, string scenarioKey)
		{
			if (!AnalysisLimits.IsIndexable(root.Count))
			{
				return null;
			}

			var pendingChoices = ParseKey(scenarioKey);
			if (pendingChoices is null)
			{
				return null;
			}

			var index = Encode(root, pendingChoices);
			return index is BigInteger value && pendingChoices.Count == 0 ? (long)value : null;
		}

		private static void Decode(StructureFragment fragment, BigInteger index, List<ScenarioChoice> choices)
		{
			if (IsChoice(fragment))
			{
				DecodeChoice(fragment, index, choices);
				return;
			}

			DecodeMixedRadix(fragment.Children, index, choices);
		}

		private static void DecodeChoice(StructureFragment fragment, BigInteger index, List<ScenarioChoice> choices)
		{
			var offset = index;
			foreach (var alternative in EnumerateAlternatives(fragment))
			{
				var branches = SelectBranches(fragment, alternative);
				var size = Product(branches);
				if (offset < size)
				{
					choices.Add(CreateChoice(fragment, branches));
					DecodeMixedRadix(branches, offset, choices);
					return;
				}

				offset -= size;
			}
		}

		private static void DecodeMixedRadix(IReadOnlyList<StructureFragment> parts, BigInteger index, List<ScenarioChoice> choices)
		{
			var digits = new BigInteger[parts.Count];
			var remaining = index;
			for (var position = parts.Count - 1; position >= 0; position--)
			{
				digits[position] = remaining % parts[position].Count;
				remaining /= parts[position].Count;
			}

			for (var position = 0; position < parts.Count; position++)
			{
				Decode(parts[position], digits[position], choices);
			}
		}

		private static BigInteger? Encode(StructureFragment fragment, Dictionary<string, IReadOnlyList<string>> pendingChoices) =>
			IsChoice(fragment) ? EncodeChoice(fragment, pendingChoices) : EncodeMixedRadix(fragment.Children, pendingChoices);

		private static BigInteger? EncodeChoice(StructureFragment fragment, Dictionary<string, IReadOnlyList<string>> pendingChoices)
		{
			if (!pendingChoices.Remove(GatewayIdOf(fragment), out var targetIds) || ResolveBranchPositions(fragment, targetIds) is not { } chosen)
			{
				return null;
			}

			var offset = BigInteger.Zero;
			foreach (var alternative in EnumerateAlternatives(fragment))
			{
				var branches = SelectBranches(fragment, alternative);
				if (alternative.SequenceEqual(chosen))
				{
					return EncodeMixedRadix(branches, pendingChoices) is BigInteger inner ? offset + inner : null;
				}

				offset += Product(branches);
			}

			return null;
		}

		private static BigInteger? EncodeMixedRadix(IReadOnlyList<StructureFragment> parts, Dictionary<string, IReadOnlyList<string>> pendingChoices)
		{
			var index = BigInteger.Zero;
			foreach (var part in parts)
			{
				if (Encode(part, pendingChoices) is not BigInteger digit)
				{
					return null;
				}

				index = index * part.Count + digit;
			}

			return index;
		}

		private static List<int>? ResolveBranchPositions(StructureFragment fragment, IReadOnlyList<string> targetIds)
		{
			var positions = targetIds
				.Select(targetId => FindBranchPosition(fragment.Children, targetId))
				.ToList();
			if (positions.Any(position => position < 0) || positions.Distinct().Count() != positions.Count)
			{
				return null;
			}

			positions.Sort();
			return positions;
		}

		private static int FindBranchPosition(IReadOnlyList<StructureFragment> branches, string targetId)
		{
			for (var position = 0; position < branches.Count; position++)
			{
				if (string.Equals(branches[position].EntryNodeId, targetId, StringComparison.Ordinal))
				{
					return position;
				}
			}

			return -1;
		}

		private static Dictionary<string, IReadOnlyList<string>>? ParseKey(string scenarioKey)
		{
			var pendingChoices = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
			if (scenarioKey.Length == 0)
			{
				return pendingChoices;
			}

			foreach (var segment in scenarioKey.Split(ScenarioKeyFormatter.ChoiceSeparator))
			{
				if (ParseChoice(segment) is not { } choice || !pendingChoices.TryAdd(choice.GatewayId, choice.TargetIds))
				{
					return null;
				}
			}

			return pendingChoices;
		}

		private static (string GatewayId, IReadOnlyList<string> TargetIds)? ParseChoice(string segment)
		{
			var parts = segment.Split(ScenarioKeyFormatter.GatewaySeparator);
			if (parts.Length != 2 || parts[0].Length == 0)
			{
				return null;
			}

			var targetIds = parts[1].Split(ScenarioKeyFormatter.TargetSeparator);
			return targetIds.Any(targetId => targetId.Length == 0) ? null : (parts[0], targetIds);
		}
	}
}
