using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerTests.Unit.Analysis
{
	public sealed class MetricTextConverterTests
	{
		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("   ")]
		public void ToDuration_Blank_ReturnsNull(string? text)
		{
			Assert.Null(MetricTextConverter.ToDuration(text, ETimeUnit.Days));
		}

		[Theory]
		[InlineData("3", "3")]
		[InlineData(" 2.5 ", "2.5")]
		[InlineData("-1", "-1")]
		public void ToDuration_PlainNumber_KeepsTrimmedText(string text, string expected)
		{
			Assert.Equal(expected, MetricTextConverter.ToDuration(text, ETimeUnit.Hours));
		}

		[Theory]
		[InlineData("15m", ETimeUnit.Minutes, "15")]
		[InlineData("2h", ETimeUnit.Minutes, "120")]
		[InlineData("90 min", ETimeUnit.Hours, "1.5")]
		[InlineData("1h 30m", ETimeUnit.Hours, "1.5")]
		[InlineData("1h30m", ETimeUnit.Minutes, "90")]
		[InlineData("3 days", ETimeUnit.Hours, "72")]
		[InlineData("2W", ETimeUnit.Days, "14")]
		[InlineData("15m", ETimeUnit.Days, "0.010417")]
		[InlineData("1.5 hours", ETimeUnit.Minutes, "90")]
		public void ToDuration_UnitSuffix_ConvertsToProjectUnit(string text, ETimeUnit timeUnit, string expected)
		{
			Assert.Equal(expected, MetricTextConverter.ToDuration(text, timeUnit));
		}

		[Theory]
		[InlineData("15 parsecs")]
		[InlineData("soon")]
		[InlineData("1,5h")]
		[InlineData("-2h")]
		public void ToDuration_UnreadableText_KeepsTextForValidation(string text)
		{
			Assert.Equal(text, MetricTextConverter.ToDuration(text, ETimeUnit.Days));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		public void ToCost_Blank_ReturnsNull(string? text)
		{
			Assert.Null(MetricTextConverter.ToCost(text, "EUR"));
		}

		[Theory]
		[InlineData("50", "EUR", "50")]
		[InlineData("25 EUR", "EUR", "25")]
		[InlineData("EUR 25.50", "EUR", "25.5")]
		[InlineData("eur25", "EUR", "25")]
		[InlineData("€100", "EUR", "100")]
		[InlineData("100€", "EUR", "100")]
		[InlineData("$50", "USD", "50")]
		[InlineData("£7.25", "GBP", "7.25")]
		public void ToCost_MatchingCurrency_ReturnsNumber(string text, string currency, string expected)
		{
			Assert.Equal(expected, MetricTextConverter.ToCost(text, currency));
		}

		[Theory]
		[InlineData("$50", "EUR")]
		[InlineData("100 USD", "EUR")]
		[InlineData("EUR 5 EUR", "EUR")]
		[InlineData("1,000 EUR", "EUR")]
		[InlineData("cheap", "EUR")]
		public void ToCost_OtherCurrencyOrUnreadable_KeepsTextForValidation(string text, string currency)
		{
			Assert.Equal(text, MetricTextConverter.ToCost(text, currency));
		}
	}
}
