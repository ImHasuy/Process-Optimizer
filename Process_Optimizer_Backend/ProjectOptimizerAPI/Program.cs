using ProjectOptimizerAPI.Lib.Collection;
using ProjectOptimizerAPI.Lib.Database;

namespace ProjectOptimizerAPI
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			builder.Services
				.AddApiControllers()
				.AddDatabase(builder.Configuration)
				.AddMappings()
				.AddApiDocumentation()
				.AddCorsPolicy(builder.Configuration)
				.AddAnalysisComponents()
				.AddLocalServices();

			var app = builder.Build();

			app.UseExceptionHandler();
			app.UseApiDocumentation();
			app.UseHttpsRedirection();
			app.UseCors(Lib.Collection.ServiceCollection.CorsPolicyName);
			app.UseAuthorization();
			app.MapControllers();

			await app.InitializeDatabaseAsync();
			await app.RunAsync();
		}
	}
}
