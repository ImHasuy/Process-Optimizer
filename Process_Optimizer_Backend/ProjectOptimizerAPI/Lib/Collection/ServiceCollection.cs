using System.Reflection;
using System.Text.Json.Serialization;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using ProjectOptimizerAPI.Lib.Analysis;
using ProjectOptimizerAPI.Lib.Database;
using ProjectOptimizerAPI.Lib.Helper;
using ProjectOptimizerAPI.Lib.Interface;
using ProjectOptimizerAPI.Lib.Middleware;
using ProjectOptimizerAPI.Lib.Repository;
using ProjectOptimizerAPI.Lib.Service;

namespace ProjectOptimizerAPI.Lib.Collection
{
	public static class ServiceCollection
	{
		public const string ConnectionStringName = "DefaultConnection";
		public const string ApiTitle = "Project Optimizer API";
		public const string CorsPolicyName = "Frontend";
		public const string CorsSectionName = "Cors:AllowedOrigins";
		private const long AnalysisCacheSizeLimit = 200_000;

		public static IServiceCollection AddApiControllers(this IServiceCollection services)
		{
			services
				.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
				.AddJsonOptions(options =>
				{
					options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
					options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
				})
				.ConfigureApiBehaviorOptions(options =>
					options.InvalidModelStateResponseFactory = context =>
						new BadRequestObjectResult(ApiResponseFactory.ValidationError(context.ModelState)));

			services.AddExceptionHandler<ApiExceptionHandler>();
			services.AddProblemDetails();
			return services;
		}

		public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddDbContext<ProjectOptimizerDbContext>(options =>
				options.UseSqlite(configuration.GetConnectionString(ConnectionStringName)));
			return services;
		}

		public static IServiceCollection AddMappings(this IServiceCollection services)
		{
			TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
			services.AddMapster();
			return services;
		}

		public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
		{
			services.AddEndpointsApiExplorer();
			services.AddSwaggerGen(options =>
			{
				options.SwaggerDoc("v1", new OpenApiInfo { Title = ApiTitle, Version = "v1" });
				options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
			});
			return services;
		}

		public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
		{
			var allowedOrigins = configuration.GetSection(CorsSectionName).Get<string[]>() ?? [];
			services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
				.WithOrigins(allowedOrigins)
				.WithHeaders("Authorization", "Content-Type")
				.AllowAnyMethod()));
			return services;
		}

		public static IServiceCollection AddAnalysisComponents(this IServiceCollection services)
		{
			services.AddMemoryCache(options => options.SizeLimit = AnalysisCacheSizeLimit);

			services.AddSingleton<IBpmnGraphParser, BpmnGraphParser>();
			services.AddSingleton<IProcessStructureAnalyzer, ProcessStructureAnalyzer>();
			services.AddSingleton<IDiagramValidator, DiagramValidator>();
			services.AddSingleton<IScenarioIndexer, ScenarioIndexer>();
			services.AddSingleton<ICriticalPathCalculator, CriticalPathCalculator>();
			services.AddSingleton<IScenarioBuilder, ScenarioBuilder>();
			services.AddSingleton<IResourceLimitCalculator, ResourceLimitCalculator>();
			services.AddSingleton<IDiagramAnalyzer, DiagramAnalyzer>();
			services.AddSingleton<IDiagramRevisionFactory, DiagramRevisionFactory>();
			services.AddSingleton<IProcessJsonConverter, ProcessJsonConverter>();
			return services;
		}

		public static IServiceCollection AddLocalServices(this IServiceCollection services)
		{
			services.AddSingleton(TimeProvider.System);

			services.AddScoped<IProjectRepository, ProjectRepository>();
			services.AddScoped<IDiagramRevisionRepository, DiagramRevisionRepository>();
			services.AddScoped<ISavedScenarioRepository, SavedScenarioRepository>();
			services.AddScoped<IUnitOfWork, UnitOfWork>();

			services.AddScoped<IProjectAccessGuard, ProjectAccessGuard>();
			services.AddScoped<IRevisionResolver, RevisionResolver>();
			services.AddScoped<IScenarioCatalog, ScenarioCatalog>();

			services.AddScoped<IProjectService, ProjectService>();
			services.AddScoped<IDiagramService, DiagramService>();
			services.AddScoped<IAnalysisService, AnalysisService>();
			services.AddScoped<IScenarioService, ScenarioService>();
			services.AddScoped<ISavedScenarioService, SavedScenarioService>();
			return services;
		}
	}
}
