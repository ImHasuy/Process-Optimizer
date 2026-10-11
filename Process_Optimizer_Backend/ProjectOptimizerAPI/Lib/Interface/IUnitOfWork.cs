namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IUnitOfWork
	{
		IProjectRepository ProjectRepository { get; }
		IDiagramRevisionRepository DiagramRevisionRepository { get; }
		ISavedScenarioRepository SavedScenarioRepository { get; }

		Task SaveChangesAsync();
		Task SaveChangesOrConflictAsync(string conflictMessage);
	}
}
