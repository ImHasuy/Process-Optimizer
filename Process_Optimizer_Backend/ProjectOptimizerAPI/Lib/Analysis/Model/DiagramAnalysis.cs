using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerLib.Enum;

namespace ProjectOptimizerAPI.Lib.Analysis.Model
{
	public sealed record DiagramAnalysis(
		string ContentSha256,
		ProcessGraph Graph,
		IReadOnlyList<ValidationIssue> Issues,
		bool IsOptimizable,
		StructureFragment? Structure)
	{
		[MemberNotNullWhen(true, nameof(Structure))]
		public bool HasOptimizableStructure => IsOptimizable && Structure is not null;

		public BigInteger ScenarioCount => Structure?.Count ?? BigInteger.Zero;

		public bool IsIndexable => AnalysisLimits.IsIndexable(ScenarioCount);

		public int ErrorCount => Issues.Count(issue => issue.Severity == EIssueSeverity.Error);

		public int WarningCount => Issues.Count(issue => issue.Severity == EIssueSeverity.Warning);
	}
}
