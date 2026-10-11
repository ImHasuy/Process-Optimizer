namespace ProjectOptimizerAPI.Lib.Analysis
{
	public static class DiagramIssueCodes
	{
		public const string ProcessCount = "DV-01";
		public const string StartEventMissing = "DV-02";
		public const string EndEventMissing = "DV-03";
		public const string UnsupportedElement = "DV-04";
		public const string DanglingSequenceFlow = "DV-05";
		public const string Unreachable = "DV-06";
		public const string DeadEnd = "DV-07";
		public const string Cycle = "DV-08";
		public const string InvalidValue = "DV-09";
		public const string MissingEstimate = "DV-10";
		public const string MixedGateway = "DV-11";
		public const string NotWellStructured = "DV-12";
		public const string ImplicitSplitOrJoin = "DV-13";
		public const string IgnoredCondition = "DV-14";
		public const string DuplicateName = "DV-15";
		public const string IgnoredSubProcessContent = "DV-16";
		public const string TooManyInclusiveBranches = "DV-17";
	}
}
