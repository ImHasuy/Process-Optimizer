using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Database;

namespace ProjectOptimizerTests.Infrastructure
{
	public sealed class SqliteTestDatabase : IDisposable
	{
		private readonly SqliteConnection _connection;

		public SqliteTestDatabase()
		{
			_connection = new SqliteConnection("Data Source=:memory:");
			_connection.Open();
			Options = new DbContextOptionsBuilder<ProjectOptimizerDbContext>().UseSqlite(_connection).Options;

			using var dbContext = CreateDbContext();
			dbContext.Database.EnsureCreated();
		}

		public DbContextOptions<ProjectOptimizerDbContext> Options { get; }

		public ProjectOptimizerDbContext CreateDbContext() => new(Options);

		public void Dispose() => _connection.Dispose();
	}
}
