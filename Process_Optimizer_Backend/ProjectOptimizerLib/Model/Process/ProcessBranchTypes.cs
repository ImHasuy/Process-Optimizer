namespace ProjectOptimizerLib.Model.Process
{
	/// <summary>The branch types the frontend exports in <see cref="ProcessBranch.Type"/>.</summary>
	public static class ProcessBranchTypes
	{
		public const string Sequence = "sequence";
		public const string Xor = "xor";
		public const string Nor = "nor";
		public const string And = "and";
		public const string Or = "or";
		public const string Complex = "complex";
	}
}
