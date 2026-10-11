using Microsoft.EntityFrameworkCore;

namespace ProjectOptimizerAPI.Lib.Database
{
	public static class DatabaseInitializer
	{
		public static async Task InitializeDatabaseAsync(this WebApplication app)
		{
			await using var scope = app.Services.CreateAsyncScope();
			await scope.ServiceProvider.GetRequiredService<ProjectOptimizerDbContext>().Database.MigrateAsync();
		}
	}
}
