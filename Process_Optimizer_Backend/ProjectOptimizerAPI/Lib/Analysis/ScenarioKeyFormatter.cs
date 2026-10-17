using ProjectOptimizerAPI.Lib.Analysis.Model;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public static class ScenarioKeyFormatter
	{
		public const char ChoiceSeparator = ';';
		public const char GatewaySeparator = '=';
		public const char TargetSeparator = '+';

		public static string Format(IReadOnlyList<ScenarioChoice> choices) =>
			string.Join(ChoiceSeparator, choices.Select(FormatChoice));

		private static string FormatChoice(ScenarioChoice choice) =>
			$"{choice.GatewayId}{GatewaySeparator}{string.Join(TargetSeparator, choice.ChosenTargetIds.Order(StringComparer.Ordinal))}";
	}
}
