namespace ProjectOptimizerAPI.Lib.Helper
{
	public static class NameNormalizer
	{
		public static string Normalize(string name) => name.Trim().ToUpperInvariant();
	}
}
