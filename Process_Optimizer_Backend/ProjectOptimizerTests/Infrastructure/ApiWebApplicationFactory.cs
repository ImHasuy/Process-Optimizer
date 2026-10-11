using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectOptimizerAPI;
using ProjectOptimizerAPI.Lib.Database;

namespace ProjectOptimizerTests.Infrastructure
{
	public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
	{
		private readonly SqliteConnection _connection = new("Data Source=:memory:");

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			_connection.Open();
			builder.UseEnvironment("Development");
			builder.ConfigureServices(services =>
			{
				services.RemoveAll<DbContextOptions<ProjectOptimizerDbContext>>();
				services.AddDbContext<ProjectOptimizerDbContext>(options => options.UseSqlite(_connection));
			});
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			_connection.Dispose();
		}
	}
}
