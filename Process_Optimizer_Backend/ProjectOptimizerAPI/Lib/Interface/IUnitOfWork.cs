namespace ProjectOptimizerAPI.Lib.Interface
{
	public interface IUnitOfWork
	{
		IProjectRepository ProjectRepository { get; }
		IDiagramRepository DiagramRepository { get; }

		Task SaveChangesAsync();
		Task SaveChangesOrConflictAsync(string conflictMessage);
	}
}
