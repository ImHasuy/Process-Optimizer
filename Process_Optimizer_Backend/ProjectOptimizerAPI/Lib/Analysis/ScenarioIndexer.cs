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
	}
}
