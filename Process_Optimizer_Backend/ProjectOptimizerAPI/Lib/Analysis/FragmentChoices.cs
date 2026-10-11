using System.Numerics;
using ProjectOptimizerAPI.Lib.Analysis.Model;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public static class FragmentChoices
	{
		public static bool IsChoice(StructureFragment fragment) =>
			fragment.Kind is EFragmentKind.Exclusive or EFragmentKind.Inclusive;

		public static string GatewayIdOf(StructureFragment fragment) => fragment.SplitNodeId ?? fragment.EntryNodeId;

		public static ScenarioChoice CreateChoice(StructureFragment fragment, IEnumerable<StructureFragment> branches) => new(
			GatewayIdOf(fragment),
			GatewayKindOf(fragment),
			fragment.IsControllable,
			branches.Select(branch => branch.EntryNodeId).Order(StringComparer.Ordinal).ToList());

		public static IEnumerable<IReadOnlyList<int>> EnumerateAlternatives(StructureFragment fragment)
		{
			var branchCount = fragment.Children.Count;
			if (fragment.Kind == EFragmentKind.Exclusive)
			{
				return Enumerable.Range(0, branchCount).Select(position => (IReadOnlyList<int>)[position]);
			}

			return Enumerable.Range(1, branchCount).SelectMany(size => EnumerateCombinations(branchCount, size, 0));
		}

		public static List<StructureFragment> SelectBranches(StructureFragment fragment, IReadOnlyList<int> positions) =>
			positions.Select(position => fragment.Children[position]).ToList();

		public static BigInteger Product(IEnumerable<StructureFragment> fragments) =>
			Multiply(fragments.Select(fragment => fragment.Count));

		public static BigInteger Multiply(IEnumerable<BigInteger> factors) =>
			factors.Aggregate(BigInteger.One, (product, factor) => product * factor);

		public static BigInteger OffsetOf(StructureFragment fragment, IReadOnlyList<int> positions) =>
			EnumerateAlternatives(fragment)
				.TakeWhile(alternative => !alternative.SequenceEqual(positions))
				.Aggregate(BigInteger.Zero, (offset, alternative) => offset + Product(SelectBranches(fragment, alternative)));

		private static EFlowNodeKind GatewayKindOf(StructureFragment fragment) =>
			fragment.GatewayKind ?? (fragment.Kind == EFragmentKind.Inclusive ? EFlowNodeKind.InclusiveGateway : EFlowNodeKind.ExclusiveGateway);

		private static IEnumerable<IReadOnlyList<int>> EnumerateCombinations(int branchCount, int size, int firstCandidate)
		{
			if (size == 0)
			{
				yield return [];
				yield break;
			}

			for (var first = firstCandidate; first <= branchCount - size; first++)
			{
				foreach (var rest in EnumerateCombinations(branchCount, size - 1, first + 1))
				{
					yield return [first, .. rest];
				}
			}
		}
	}
}
