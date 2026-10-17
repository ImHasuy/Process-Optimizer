# Configuration and running

## Prerequisites

- .NET SDK 8.0 (the build used SDK 8.0.425; all projects target `net8.0`).
- No database server. SQLite runs in-process.
- No global tools. `dotnet-ef` 8.0.31 is a local tool restored from `.config/dotnet-tools.json`.

## appsettings keys

| Key | `appsettings.json` | `appsettings.Development.json` | Used by |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | `Data Source=projectoptimizer.db` | – | `AddDatabase` |
| `Cors:AllowedOrigins` | `[]` | `["http://localhost:5173", "http://localhost:3000"]` | `AddCorsPolicy` (policy `Frontend`) |
| `Logging:LogLevel:Default` | `Information` | `Information` | logging |
| `Logging:LogLevel:Microsoft.AspNetCore` | `Warning` | `Warning` | logging |
| `AllowedHosts` | `*` | – | host filtering |

Any key can be overridden with environment variables, for example
`ConnectionStrings__DefaultConnection="Data Source=/tmp/other.db"` or `Cors__AllowedOrigins__0=http://localhost:4200`.

Constants that are code, not configuration:

- `ServiceCollection.ApiTitle = "Project Optimizer API"`, `CorsPolicyName = "Frontend"`, and the memory-cache size
  limit 200 000.
- `AnalysisLimits` (see [services.md](services.md#limits-analysislimits)).
- `FieldLimits` (see [dtos-and-mapping.md](dtos-and-mapping.md)).

## Launch profiles and ports

`ProjectOptimizerAPI/Properties/launchSettings.json`:

| Profile | URLs | Environment | Opens |
|---|---|---|---|
| `http` (default) | `http://localhost:5212` | `Development` | `swagger` |
| `https` | `https://localhost:7236`, `http://localhost:5212` | `Development` | `swagger` |

Swagger UI is at `http://localhost:5212/swagger`, and the OpenAPI document at
`http://localhost:5212/swagger/v1/swagger.json`. Both exist only in `Development` (`UseApiDocumentation`).
`app.UseHttpsRedirection()` is in the pipeline; with the `http` profile no HTTPS port is configured, so requests
stay on HTTP.

`ProjectOptimizerAPI/ProjectOptimizerAPI.http` contains a request for every endpoint against `http://localhost:5212`,
in the order of a typical session. Run "Create project" first and put the returned id into `@projectId`.

## Startup sequence

`Program.Main` performs these steps:

1. Registers the services: `AddApiControllers` → `AddDatabase` → `AddMappings` → `AddApiDocumentation` →
   `AddCorsPolicy` → `AddAnalysisComponents` → `AddLocalServices`.
2. Builds the pipeline: `UseExceptionHandler` → `UseApiDocumentation` → `UseHttpsRedirection` →
   `UseCors("Frontend")` → `UseAuthorization` → `MapControllers`.
3. Runs `InitializeDatabaseAsync()`, which applies the migrations with `MigrateAsync()` (there is no seed data).
4. Runs `RunAsync()`.

## Commands

Run all commands from the repository root.

| Task | Command |
|---|---|
| Restore packages | `dotnet restore` |
| Restore the local `dotnet-ef` | `dotnet tool restore` |
| Build (zero warnings) | `dotnet build ProjectOptimizer.sln -warnaserror` |
| Run the tests | `dotnet test ProjectOptimizer.sln` |
| Run the API (`http` profile) | `dotnet run --project ProjectOptimizerAPI` |
| Run with the `https` profile | `dotnet run --project ProjectOptimizerAPI --launch-profile https` |
| Run on another URL | `dotnet run --project ProjectOptimizerAPI --no-launch-profile --urls http://localhost:5300` (set `ASPNETCORE_ENVIRONMENT=Development` for Swagger) |
| List migrations | `dotnet ef migrations list --project ProjectOptimizerAPI` |
| Add a migration | `dotnet ef migrations add <Name> --project ProjectOptimizerAPI --output-dir Migrations` |
| Apply migrations manually | `dotnet ef database update --project ProjectOptimizerAPI` (the app also applies them on startup) |
| Reset the local database | stop the API, delete `ProjectOptimizerAPI/projectoptimizer.db*`, start again (the database starts empty). Required once for a database created before the migrations were regenerated as a single `InitialCreate` |

## Environment notes

- **Phase 1 has no authentication.** Run it only locally or on a trusted network; do not expose it to the
  internet.
- **The memory cache is per process.** Restarting the API clears it; nothing in it needs to survive a restart.
- **The SQLite database file** is the only persistent state.
