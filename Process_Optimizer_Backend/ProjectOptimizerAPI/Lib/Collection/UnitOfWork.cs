using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Exceptions;
using ProjectOptimizerAPI.Lib.Interface;

namespace ProjectOptimizerAPI.Lib.Collection
{
	public sealed class UnitOfWork(
		ProjectOptimizerDbContext dbContext,
		IProjectRepository projectRepository,
		IDiagramRevisionRepository diagramRevisionRepository,
		ISavedScenarioRepository savedScenarioRepository) : IUnitOfWork
	{
		private const int SqliteConstraintUnique = 2067;
		private const int SqliteConstraintPrimaryKey = 1555;

		public IProjectRepository ProjectRepository { get; } = projectRepository;
		public IDiagramRevisionRepository DiagramRevisionRepository { get; } = diagramRevisionRepository;
		public ISavedScenarioRepository SavedScenarioRepository { get; } = savedScenarioRepository;

		public Task SaveChangesAsync() => dbContext.SaveChangesAsync();

		public async Task SaveChangesOrConflictAsync(string conflictMessage)
		{
			try
			{
				await SaveChangesAsync();
			}
			catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
			{
				throw new ConflictException(conflictMessage);
			}
		}

		private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
			exception.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique or SqliteConstraintPrimaryKey };
	}
}
