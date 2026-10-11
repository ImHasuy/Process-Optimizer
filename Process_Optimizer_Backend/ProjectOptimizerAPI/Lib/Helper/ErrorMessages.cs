namespace ProjectOptimizerAPI.Lib.Helper
{
	public static class ErrorMessages
	{
		public const string RevisionNotFound = "The diagram revision does not exist.";
		public const string ScenarioNotFound = "The scenario does not exist for this revision.";
		public const string DiagramHasErrors = "The diagram has errors and cannot be analysed.";
		public const string TooManyScenarios = "The diagram has too many scenarios to index.";
	}
}
