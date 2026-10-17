using System.Numerics;

namespace ProjectOptimizerAPI.Lib.Analysis
{
	public static class AnalysisLimits
	{
		public const long MaxSafeInteger = 9007199254740991;
		public const int MaxXmlMegabytes = 2;
		public const int MaxXmlBytes = MaxXmlMegabytes * 1024 * 1024;
		public const int MaxFlowNodes = 1000;
		public const long MaxIndexableScenarios = MaxSafeInteger;
		public const int MaxInclusiveBranches = 10;
		public const int MaxListedScenarios = 1000;
		public const int MinBranchingFlows = 2;
		public const int OrderingVersion = 1;
		public const int MaxSaveRequestBytes = 3 * 1024 * 1024;
		public const long MaxPathCount = MaxSafeInteger;

		public static bool IsIndexable(BigInteger scenarioCount) => scenarioCount <= MaxIndexableScenarios;

		public static bool IsIndexInRange(BigInteger index, BigInteger scenarioCount) => index >= 0 && index < scenarioCount;
	}
}
