namespace ProjectOptimizerAPI.Lib.Helper
{
	public static class DecimalNormalizer
	{
		public static decimal Normalize(decimal value) => value / 1.0000000000000000000000000000m;
	}
}
