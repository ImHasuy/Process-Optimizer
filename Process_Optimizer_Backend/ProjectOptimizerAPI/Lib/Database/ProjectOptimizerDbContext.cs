using Microsoft.EntityFrameworkCore;
using ProjectOptimizerLib.Model;

namespace ProjectOptimizerAPI.Lib.Database
{
	public class ProjectOptimizerDbContext(DbContextOptions<ProjectOptimizerDbContext> options) : DbContext(options)
	{
		public DbSet<Project> Projects => Set<Project>();
		public DbSet<DiagramRevision> DiagramRevisions => Set<DiagramRevision>();
		public DbSet<SavedScenario> SavedScenarios => Set<SavedScenario>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProjectOptimizerDbContext).Assembly);
		}
	}
}
