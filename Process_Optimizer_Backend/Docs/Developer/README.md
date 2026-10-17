# Project Optimizer API — Developer guide

This guide is for developers who maintain the **Project Optimizer API** (phase 1). It explains how the code is
built, what each file, class and method does, how the data is stored, and how to change the code safely.

## Purpose

The API stores projects. Each project has exactly one BPMN 2.0 process diagram; saving a diagram overwrites it (there
is no version history). A diagram arrives either as BPMN XML or as the frontend's JSON export (`states` and
`branches`), which is converted to BPMN XML. The API parses and validates the diagram on every save and stores its
error and warning counts. When a diagram is well structured, the API lists its
*scenarios*: each scenario is one combination of exclusive or inclusive gateway choices. For every scenario the API
computes the critical path (CPM schedule), the total duration and the total cost. It also computes the cost and
duration limits over all scenarios.

Phase 1 has **no authentication, no project ownership and no optimizer**. Those parts belong
to phase 2. Every endpoint is anonymous and project names are globally unique. Do not expose this build to the
internet.

## Tech stack

The versions come from the csproj files and `.config/dotnet-tools.json`.

| Component | Version | Where |
|---|---|---|
| .NET / ASP.NET Core | `net8.0` (SDK 8.0.425 used by the build) | all three projects |
| Entity Framework Core + SQLite provider | `Microsoft.EntityFrameworkCore.Sqlite` 8.0.31 | `ProjectOptimizerAPI`, `ProjectOptimizerTests` |
| EF Core design-time | `Microsoft.EntityFrameworkCore.Design` 8.0.31 | `ProjectOptimizerAPI` |
| `dotnet-ef` local tool | 8.0.31 | `.config/dotnet-tools.json` |
| Mapster | 10.0.13 (+ `Mapster.DependencyInjection` 10.0.13) | `ProjectOptimizerAPI` |
| Swashbuckle (Swagger UI + OpenAPI) | `Swashbuckle.AspNetCore` 10.3.0 | `ProjectOptimizerAPI` |
| In-memory cache | `Microsoft.Extensions.Caching.Memory` (part of the shared framework) | `ProjectOptimizerAPI` |
| xUnit | 2.5.3 (+ `xunit.runner.visualstudio` 2.5.3, `Microsoft.NET.Test.Sdk` 17.8.0) | `ProjectOptimizerTests` |
| ASP.NET Core integration testing | `Microsoft.AspNetCore.Mvc.Testing` 8.0.31 | `ProjectOptimizerTests` |
| Fake time | `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 | `ProjectOptimizerTests` |
| Coverage collector | `coverlet.collector` 6.0.0 | `ProjectOptimizerTests` |

## Index

| File | Content |
|---|---|
| [architecture.md](architecture.md) | Layers, request flow diagram, project split, cross-cutting concerns, design decisions |
| [project-structure.md](project-structure.md) | Full folder tree and a file → responsibility table |
| [controllers.md](controllers.md) | Every controller, every action → route → service method → status codes |
| [services.md](services.md) | Every service and service helper, with each method, its rules and its exceptions, plus the analysis engine components (BPMN parser, validator, structure tree, scenario indexing, CPM, resource limits, caching) |
| [repositories-and-unit-of-work.md](repositories-and-unit-of-work.md) | `Repository<TEntity>`, the two repositories, the unit of work and the transaction boundary |
| [dtos-and-mapping.md](dtos-and-mapping.md) | Every DTO with its fields and validation, the frontend JSON format classes, and the Mapster configurations |
| [database.md](database.md) | SQLite, ER diagram, tables, indexes, cascade rules, migrations, resetting the database |
| [validation-and-errors.md](validation-and-errors.md) | Formal and business validation, exceptions → status codes, the full rule list |
| [testing.md](testing.md) | Test project layout, infrastructure, how to run the tests, what each test class covers |
| [configuration-and-running.md](configuration-and-running.md) | appsettings keys, launch profiles, ports, `dotnet` commands |
| [extending.md](extending.md) | Step-by-step recipe for adding a new entity or feature |

There is no `authentication.md` because phase 1 has no authentication.

The API consumer guide is in [../EndUser/README.md](../EndUser/README.md).

## How a request flows

1. Kestrel receives the request. The pipeline in `Program.cs` runs in this order: exception handler → Swagger
   (Development only) → HTTPS redirection → CORS policy `Frontend` → authorization (no policies) → controllers.
2. Routing selects an action on a controller that derives from `ApiControllerBase`. Route constraints
   (`{id:guid}`, `{projectId:guid}`, `{index:long}`) reject malformed ids before any code runs. The result is
   an empty 404 from routing, not an envelope.
3. `[ApiController]` binds the body or query DTO and runs the Data Annotations. If validation fails,
   `InvalidModelStateResponseFactory` returns a 400 `ApiResponse` that contains an `errors` dictionary.
4. The action calls one `I*Service` method. Controllers contain no logic.
5. The service checks that the project exists through `IProjectAccessGuard`. Analysis requests also go through
   `IDiagramResolver` and `IScenarioCatalog`. Business rules are enforced by small `Ensure…` guard methods.
6. Data access goes through `IUnitOfWork` → `I*Repository` → `ProjectOptimizerDbContext` (SQLite). Read queries
   use `AsNoTracking()` and Mapster `ProjectToType<T>()` projections.
7. Diagram work (parse → validate → structure tree → scenarios → CPM → limits) is done by the singleton components
   in `Lib/Analysis`, behind `IDiagramAnalyzer`. The results are cached in `IMemoryCache`, keyed by the SHA-256
   hash of the XML.
8. Writes are committed once per request with `SaveChangesAsync()`, or with `SaveChangesOrConflictAsync(message)`,
   which turns a unique-index violation into a 409.
9. The controller wraps the result with `Success(...)` (200) or `Created(...)` (201 + `Location`), or returns
   `NoContent()` (204). The one exception is the `.bpmn` file download, which returns a raw XML file.
10. Any exception goes to `ApiExceptionHandler`. `ApiException` subclasses become 400/404/409,
    `BadHttpRequestException` becomes its own status (for example 413), and everything else becomes 500.
    Each of these is returned as an `ApiResponse` envelope.
