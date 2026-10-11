using Microsoft.EntityFrameworkCore;
using ProjectOptimizerAPI.Lib.Interface;

namespace ProjectOptimizerAPI.Lib.Database
{
	public static class DatabaseInitializer
	{
		public static async Task InitializeDatabaseAsync(this WebApplication app)
		{
			await using var scope = app.Services.CreateAsyncScope();
			var dbContext = scope.ServiceProvider.GetRequiredService<ProjectOptimizerDbContext>();
			var revisionFactory = scope.ServiceProvider.GetRequiredService<IDiagramRevisionFactory>();
			var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

			await dbContext.Database.MigrateAsync();
			await DatabaseSeeder.SeedAsync(dbContext, revisionFactory, timeProvider);
		}
	}
}
