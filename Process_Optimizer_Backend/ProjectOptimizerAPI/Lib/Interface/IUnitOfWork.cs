namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IUnitOfWork
	{
		IProjectRepository ProjectRepository { get; }
		IDiagramRepository DiagramRepository { get; }
		ISavedScenarioRepository SavedScenarioRepository { get; }

		Task SaveChangesAsync();
		Task SaveChangesOrConflictAsync(string conflictMessage);
	}
}
