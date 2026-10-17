using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Helper
{
	public static partial class MetricTextConverter
	{
		private const int Decimals = 6;

		private static readonly FrozenDictionary<string, ETimeUnit> TimeUnitsBySuffix = new Dictionary<string, ETimeUnit>
		{
			["m"] = ETimeUnit.Minutes,
			["min"] = ETimeUnit.Minutes,
			["mins"] = ETimeUnit.Minutes,
			["minute"] = ETimeUnit.Minutes,
			["minutes"] = ETimeUnit.Minutes,
			["h"] = ETimeUnit.Hours,
			["hr"] = ETimeUnit.Hours,
			["hrs"] = ETimeUnit.Hours,
			["hour"] = ETimeUnit.Hours,
			["hours"] = ETimeUnit.Hours,
			["d"] = ETimeUnit.Days,
			["day"] = ETimeUnit.Days,
			["days"] = ETimeUnit.Days,
			["w"] = ETimeUnit.Weeks,
			["wk"] = ETimeUnit.Weeks,
			["wks"] = ETimeUnit.Weeks,
			["week"] = ETimeUnit.Weeks,
			["weeks"] = ETimeUnit.Weeks
		}.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

		private static readonly FrozenDictionary<ETimeUnit, decimal> MinutesPerUnit = new Dictionary<ETimeUnit, decimal>
		{
			[ETimeUnit.Minutes] = 1m,
			[ETimeUnit.Hours] = 60m,
			[ETimeUnit.Days] = 1440m,
			[ETimeUnit.Weeks] = 10080m
		}.ToFrozenDictionary();

		private static readonly FrozenDictionary<string, string> CurrencyCodesBySymbol = new Dictionary<string, string>
		{
			["$"] = "USD",
			["€"] = "EUR",
			["£"] = "GBP"
		}.ToFrozenDictionary(StringComparer.Ordinal);

		public static string? ToDuration(string? text, ETimeUnit timeUnit)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}

			var trimmed = text.Trim();
			if (IsPlainNumber(trimmed))
			{
				return trimmed;
			}

			return TryConvertDuration(trimmed, timeUnit, out var converted) ? converted : trimmed;
		}

		public static string? ToCost(string? text, string currency)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}

			var trimmed = text.Trim();
			if (IsPlainNumber(trimmed))
			{
				return trimmed;
			}

			return TryConvertCost(trimmed, currency, out var converted) ? converted : trimmed;
		}

		private static bool IsPlainNumber(string text) =>
			decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

		private static bool TryConvertDuration(string text, ETimeUnit timeUnit, out string converted)
		{
			converted = text;
			var match = DurationPattern().Match(text);
			if (!match.Success)
			{
				return false;
			}

			var values = match.Groups["value"].Captures;
			var units = match.Groups["unit"].Captures;
			var totalMinutes = 0m;
			for (var position = 0; position < values.Count; position++)
			{
				if (!TimeUnitsBySuffix.TryGetValue(units[position].Value, out var unit))
				{
					return false;
				}

				totalMinutes += ParseInvariant(values[position].Value) * MinutesPerUnit[unit];
			}

			converted = Format(totalMinutes / MinutesPerUnit[timeUnit]);
			return true;
		}

		private static bool TryConvertCost(string text, string currency, out string converted)
		{
			converted = text;
			var match = CostPattern().Match(text);
			if (!match.Success)
			{
				return false;
			}

			var prefix = match.Groups["prefix"].Value;
			var suffix = match.Groups["suffix"].Value;
			if (prefix.Length > 0 == suffix.Length > 0 || !string.Equals(CurrencyCodeOf(prefix + suffix), currency, StringComparison.Ordinal))
			{
				return false;
			}

			converted = Format(ParseInvariant(match.Groups["value"].Value));
			return true;
		}

		private static string CurrencyCodeOf(string marker) =>
			CurrencyCodesBySymbol.TryGetValue(marker, out var code) ? code : marker.ToUpperInvariant();

		private static decimal ParseInvariant(string value) => decimal.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

		private static string Format(decimal value) =>
			DecimalNormalizer.Normalize(Math.Round(value, Decimals, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

		[GeneratedRegex(@"^(?:\s*(?<value>\d+(?:\.\d+)?)\s*(?<unit>[A-Za-z]+))+\s*$", RegexOptions.CultureInvariant)]
		private static partial Regex DurationPattern();

		[GeneratedRegex(@"^(?<prefix>[A-Za-z]{3}|[$€£])?\s*(?<value>\d+(?:\.\d+)?)\s*(?<suffix>[A-Za-z]{3}|[$€£])?$", RegexOptions.CultureInvariant)]
		private static partial Regex CostPattern();
	}
}
