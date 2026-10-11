using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using ProjectOptimizerAPI.Lib.Collection;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerAPI.Lib.Repository;
using ProjectOptimizerAPI.Lib.Service;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerTests.Infrastructure
{
	public abstract class ServiceTestBase : IDisposable
	{
		protected static readonly DateTimeOffset FixedNow = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

		private readonly SqliteTestDatabase _database = new();

		static ServiceTestBase()
		{
			TypeAdapterConfig.GlobalSettings.Scan(typeof(UnitOfWork).Assembly);
		}

		protected ServiceTestBase()
		{
			DbContext = _database.CreateDbContext();
			UnitOfWork = new UnitOfWork(
				DbContext,
				new ProjectRepository(DbContext),
				new DiagramRevisionRepository(DbContext),
				new SavedScenarioRepository(DbContext));
			Analysis = AnalysisTestComponents.Create();
			ProjectAccessGuard = new ProjectAccessGuard(UnitOfWork);
			RevisionResolver = new RevisionResolver(UnitOfWork, ProjectAccessGuard);
			ScenarioCatalog = new ScenarioCatalog(RevisionResolver, Analysis.Analyzer, Analysis.Indexer);
			TestData = new TestDataBuilder(DbContext, FixedNow.UtcDateTime, Analysis.RevisionFactory);
		}

		protected ProjectOptimizerDbContext DbContext { get; }
		protected IUnitOfWork UnitOfWork { get; }
		protected FakeTimeProvider TimeProvider { get; } = new(FixedNow);
		protected AnalysisTestComponents Analysis { get; }
		protected IProjectAccessGuard ProjectAccessGuard { get; }
		protected IRevisionResolver RevisionResolver { get; }
		protected IScenarioCatalog ScenarioCatalog { get; }
		protected TestDataBuilder TestData { get; }

		protected Task<Project> LoadProjectAsync(Guid id) => DbContext.Projects.AsNoTracking().SingleAsync(project => project.Id == id);

		public void Dispose()
		{
			DbContext.Dispose();
			_database.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
