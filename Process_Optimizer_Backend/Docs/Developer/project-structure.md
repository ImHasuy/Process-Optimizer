# Project structure

## Folder tree

```
Process_Optimizer_Backend/
├── ProjectOptimizer.sln
├── .config/dotnet-tools.json            local dotnet-ef 8.0.31
├── .gitignore
├── README.md
├── Docs/
│   ├── EndUser/                         API consumer guide
│   └── Developer/                       this guide
├── ProjectOptimizerAPI/
│   ├── Controllers/
│   ├── Lib/
│   │   ├── Analysis/
│   │   │   └── Model/
│   │   ├── Collection/
│   │   ├── Database/
│   │   │   └── Configuration/
│   │   ├── Exceptions/
│   │   ├── Helper/
│   │   ├── Interface/
│   │   ├── Mapping/
│   │   ├── Middleware/
│   │   ├── Repository/
│   │   └── Service/
│   ├── Migrations/
│   ├── Properties/launchSettings.json
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── ProjectOptimizerAPI.http
│   ├── ProjectOptimizerAPI.csproj
│   └── Program.cs
├── ProjectOptimizerLib/
│   ├── DTO/
│   ├── Enum/
│   ├── Model/
│   │   └── Process/
│   └── ProjectOptimizerLib.csproj
└── ProjectOptimizerTests/
    ├── Fixtures/
    │   ├── Parsing/
    │   ├── Validation/
    │   └── worked-example.bpmn
    ├── Infrastructure/
    ├── Integration/Controller/
    ├── Unit/Analysis/
    ├── Unit/Service/
    └── ProjectOptimizerTests.csproj
```

## Root files

| File | Responsibility |
|---|---|
| `ProjectOptimizer.sln` | Solution with the three projects |
| `.config/dotnet-tools.json` | Pins the local `dotnet-ef` tool (8.0.31); restore it with `dotnet tool restore` |
| `.gitignore` | Ignores `bin/`, `obj/`, `.vs/`, `*.user`, SQLite files (`*.db`, `*.db-shm`, `*.db-wal`), workflow logs and `TestResults/` |
| `README.md` | Repository overview |

## `ProjectOptimizerAPI`

| File | Responsibility |
|---|---|
| `Program.cs` | Builds the host. Registers the service groups (`AddApiControllers`, `AddDatabase`, `AddMappings`, `AddApiDocumentation`, `AddCorsPolicy`, `AddAnalysisComponents`, `AddLocalServices`), configures the pipeline, applies the migrations, and runs the app |
| `ProjectOptimizerAPI.csproj` | Web SDK, `net8.0`, XML documentation file (warning 1591 suppressed), package references |
| `ProjectOptimizerAPI.http` | One ready-made request per endpoint, in the order of a typical session (host `http://localhost:5212`) |
| `appsettings.json` | Connection string, empty CORS origin list, logging, `AllowedHosts` |
| `appsettings.Development.json` | CORS origins `http://localhost:5173` and `http://localhost:3000`, logging |
| `Properties/launchSettings.json` | `http` profile (`http://localhost:5212`) and `https` profile (`https://localhost:7236;http://localhost:5212`); both open `swagger` |

### `Controllers/`

| File | Responsibility |
|---|---|
| `ApiControllerBase.cs` | `[ApiController]`, `[Produces("application/json")]`; the helpers `Success<T>(data)` → 200 envelope and `Created<T>(action, routeValues, data)` → 201 envelope with `Location` |
| `ProjectController.cs` | Project CRUD and list (`/api/projects`) |
| `DiagramController.cs` | Get the diagram, overwrite it (BPMN XML or frontend JSON), `.bpmn` download (`/api/projects/{projectId}/diagram`) |
| `ScenarioController.cs` | Scenario count, all scenarios, single scenario, resource limits |

### `Lib/Analysis/` — diagram analysis engine

| File | Responsibility |
|---|---|
| `AnalysisLimits.cs` | All numeric limits: 2 MB XML, 1000 flow nodes, 2^53 − 1 indexable scenarios, 10 inclusive branches, 1000 listed scenarios, ordering version 1, 3 MiB request size |
| `BpmnNamespaces.cs` | Namespace URIs: BPMN model, BPMN DI, DC, DI, XSI, `http://projectoptimizer.app/schema/bpmn/1.0` (prefix `po`) and the frontend's `http://process-optimizer.com/schema/1.0/bpmn` (prefix `optimizer`) |
| `BpmnGraphParser.cs` | Safe XML loading (no DTD), size and node limits, reads the single process into a `ProcessGraph`, and reports parse issues DV-01/04/05/09/16 |
| `DiagramIssueCodes.cs` | Constants `DV-01` … `DV-17` |
| `DiagramValidator.cs` | Graph checks DV-02/03/06/07/08/10/11/13/14/15/17, runs the structure analysis (DV-12) when no error was found, sorts the issues |
| `ProcessStructureAnalyzer.cs` | Builds the single-entry single-exit `StructureFragment` tree with scenario counts, or reports DV-12 |
| `FragmentChoices.cs` | Shared helpers for exclusive/inclusive fragments: alternatives, branch selection, products, offsets, `ScenarioChoice` creation |
| `ScenarioIndexer.cs` | `Unrank` (index → choices and key) |
| `ScenarioKeyFormatter.cs` | Key format `gatewayId=t1+t2;gatewayId2=t3` |
| `ScenarioBuilder.cs` | Builds a scenario sub-graph from a selection, runs CPM, computes totals, and maps to `ScenarioDTO` |
| `CriticalPathCalculator.cs` | CPM forward/backward pass, slack, critical path trace |
| `ResourceLimitCalculator.cs` | Min/max cost, min/max critical-path duration, max sequential and exclusive durations, with the scenarios that reach them |
| `TopologicalOrder.cs` | Kahn's algorithm with a deterministic priority (start events first, end events last, then `bpmnId` ordinal) |
| `GraphTraversal.cs` | Breadth-first reachability |
| `DiagramAnalyzer.cs` | Façade over parse + validate + index + build; owns all `IMemoryCache` entries |
| `DiagramFactory.cs` | `Create` builds a new `Diagram` entity and `Apply` overwrites an existing one: hash, size, node count, issue counts, `IsOptimizable`, planned `MaxCost` / `MaxDurationBound`, `UpdatedAt`. It analyses the XML before mutating, so a rejected XML leaves the entity unchanged |
| `ProcessJsonConverter.cs` | Converts a `ProcessDiagram` (the frontend JSON export) to BPMN XML with a generated layout |
| `ProcessJsonLayout.cs` | Column/row layout of the converted nodes (`Model/LayoutBounds`) |
| `DefaultDiagram.cs` | The default BPMN XML of a new project (a single start event) |

### `Lib/Analysis/Model/` — in-memory records (never persisted, never returned directly)

| File | Responsibility |
|---|---|
| `ProcessGraph.cs` | Process id, nodes, edges, nodes by id, flow-node count, parse issues |
| `FlowNode.cs` | One BPMN node with kind, duration, cost, parallelizable, estimate, crash options, dependencies; the helpers `IsActivity`, `IsMissingEstimate`, `IsGateway` |
| `SequenceFlowEdge.cs` | One sequence flow (id, source, target, name, condition, is default) |
| `Estimate.cs`, `CrashOption.cs` | Optional `po:estimate` and `po:crashOption` extension values |
| `ValidationIssue.cs` | Code, severity, element id, message |
| `DiagramValidationResult.cs` | Issues, `IsOptimizable`, structure tree |
| `StructureAnalysisResult.cs` | Root fragment or `null`, plus the DV-12 issues |
| `StructureFragment.cs` | Tree node: kind, entry/node/split/join ids, gateway kind, controllable, children, `BigInteger Count` |
| `DiagramAnalysis.cs` | Cached analysis result: hash, graph, issues, `IsOptimizable`, structure; derived `HasOptimizableStructure`, `ScenarioCount`, `IsIndexable`, `ErrorCount`, `WarningCount` |
| `ScenarioChoice.cs`, `ScenarioSelection.cs` | One gateway decision; index + key + choices |
| `ScenarioComputation.cs` | A built scenario: element ids, flow ids, scheduled activities, critical path, path count, totals, flags |
| `ScheduledNode.cs`, `CriticalPathResult.cs` | CPM output per node; schedule + critical path + makespan |
| `NodeNetwork.cs` | Positional adjacency lists used by CPM and path counting |
| `ProjectContext.cs` | Project id, time unit, currency (added to DTOs after the cache) |
| `ResolvedDiagram.cs` | Context, XML, hash |
| `AnalyzedDiagram.cs`, `IndexedDiagram.cs` | Resolved diagram (`Diagram`) + analysis (+ `long` scenario count and `Contains(index)`) |

### `Lib/Collection/`

| File | Responsibility |
|---|---|
| `ServiceCollection.cs` | DI extension methods: controllers + JSON + validation envelope + exception handler; DbContext; Mapster; Swagger; CORS; analysis singletons + memory cache; repositories, unit of work, helpers, services, `TimeProvider` |
| `ApplicationBuilderExtensions.cs` | `UseApiDocumentation()` — Swagger JSON + UI in Development only |
| `UnitOfWork.cs` | Exposes the two repositories; `SaveChangesAsync()`; `SaveChangesOrConflictAsync(message)` maps SQLite unique/primary-key violations to `ConflictException` |

### `Lib/Database/`

| File | Responsibility |
|---|---|
| `ProjectOptimizerDbContext.cs` | `DbSet`s `Projects`, `Diagrams`; applies the configurations from the assembly |
| `Configuration/ProjectConfiguration.cs` | Table `Projects`, lengths, enum-as-string, unique `NormalizedName`, index on `UpdatedAt` |
| `Configuration/DiagramConfiguration.cs` | Table `Diagrams`, primary key `ProjectId`, one-to-one with `Project.Diagram`, cascade from project |
| `DatabaseInitializer.cs` | `InitializeDatabaseAsync()` — applies the migrations with `MigrateAsync()`; there is no seed data |

### `Lib/Exceptions/`

| File | Responsibility |
|---|---|
| `ApiException.cs` | Abstract base with `StatusCode` |
| `InvalidRequestException.cs` | 400 |
| `NotFoundException.cs` | 404; `For<TEntity>(id)` → `"<Entity> with id '<id>' was not found."` |
| `ConflictException.cs` | 409 |

### `Lib/Helper/`

| File | Responsibility |
|---|---|
| `ApiResponseFactory.cs` | Builds success, error and validation-error envelopes; message constants |
| `ErrorMessages.cs` | Shared messages: diagram not found, scenario not found, diagram has errors, too many scenarios, too many scenarios to list |
| `NameNormalizer.cs` | `Trim().ToUpperInvariant()` |
| `ProjectFactory.cs` | Creates `Project` entities; holds the defaults `Days` and `EUR` |
| `ContentHash.cs` | Lower-case hex SHA-256 of the UTF-8 XML |
| `DecimalNormalizer.cs` | Removes trailing zeros from decimals |
| `MetricTextConverter.cs` | Converts time/price texts (`15m`, `1h 30m`, `25 EUR`, `€25`) to numbers in the project unit and currency |
| `TimeProviderExtensions.cs` | `UtcNow()` → UTC `DateTime` |

### `Lib/Interface/`

| File | Responsibility |
|---|---|
| `IRepository.cs`, `IProjectRepository.cs`, `IDiagramRepository.cs`, `IUnitOfWork.cs` | Data-access contracts |
| `IProjectService.cs`, `IDiagramService.cs`, `IScenarioService.cs` | Service contracts used by the controllers |
| `IProjectAccessGuard.cs`, `IDiagramResolver.cs`, `IScenarioCatalog.cs` | Shared service helpers |
| `IBpmnGraphParser.cs`, `IDiagramValidator.cs`, `IProcessStructureAnalyzer.cs`, `IScenarioIndexer.cs`, `ICriticalPathCalculator.cs`, `IScenarioBuilder.cs`, `IResourceLimitCalculator.cs`, `IDiagramAnalyzer.cs`, `IDiagramFactory.cs`, `IProcessJsonConverter.cs` | Analysis engine contracts |

### `Lib/Mapping/`, `Lib/Middleware/`, `Lib/Repository/`, `Lib/Service/`

| File | Responsibility |
|---|---|
| `Mapping/ProjectMappingConfig.cs` | `Project` → `ProjectListItemDTO` / `ProjectDetailsDTO` with values from `Project.Diagram` |
| `Middleware/ApiExceptionHandler.cs` | Exceptions → envelope + logging |
| `Repository/Repository.cs` | Generic base repository |
| `Repository/ProjectRepository.cs` | Project list (newest update first), name check |
| `Repository/DiagramRepository.cs` | The project's diagram as tracked entity (`FindAsync`) or `DiagramDTO` (`FindDtoAsync`); does not derive from `Repository<T>` |
| `Service/ProjectService.cs` | Project use cases |
| `Service/DiagramService.cs` | Get, overwrite (XML or JSON) and download the diagram |
| `Service/ScenarioService.cs` | Count, all scenarios, single scenario, resource limits |
| `Service/ProjectAccessGuard.cs` | Project existence guard (phase-2 ownership hook) |
| `Service/DiagramResolver.cs` | Project id → diagram XML + hash + project context |
| `Service/ScenarioCatalog.cs` | Analysis (private) + BR-11 / BR-12 guards |

### `Migrations/`

| File | Responsibility |
|---|---|
| `20261012193947_InitialCreate.cs` (+ `.Designer.cs`), `ProjectOptimizerDbContextModelSnapshot.cs` | Generated by `dotnet-ef`: the two tables, the foreign key and the indexes |

## `ProjectOptimizerLib`

| File | Responsibility |
|---|---|
| `ProjectOptimizerLib.csproj` | Class library, `net8.0`, no packages |
| `Model/Project.cs`, `Model/Diagram.cs` | EF entity POCOs |
| `Model/Process/ProcessDiagram.cs` | The frontend JSON export (`States`, `Branches`); `Parse(json)` and `ToJson()` with web JSON defaults |
| `Model/Process/ProcessState.cs` | One state: `Id`, `Name`, `Type`, `Time`, `Price`, `DependsOn` (`Depends_on`), `ConnectsTo` (`Connects_to`), with Data Annotations |
| `Model/Process/ProcessBranch.cs` | One branch: `Id`, `Name`, `Source`, `Target`, `Type`, `Condition`; `IsDefault` when the type is `nor` |
| `Model/Process/ProcessBranchTypes.cs` | Branch type constants `sequence`, `xor`, `nor`, `and`, `or`, `complex` |
| `Enum/ETimeUnit.cs`, `EResourceLimitBasis.cs`, `EIssueSeverity.cs`, `EFlowNodeKind.cs`, `EGatewayDirection.cs`, `EFragmentKind.cs` | Enums (see [dtos-and-mapping.md](dtos-and-mapping.md#enums)) |
| `DTO/ApiResponse.cs` | Envelope `ApiResponse` and `ApiResponse<TData>` |
| `DTO/FieldLimits.cs` | Name 100, text 1000, BPMN id 255, BPMN type 100, time/price text 100 |
| `DTO/TrimmedStringLengthAttribute.cs` | Length check after trimming |
| `DTO/*DTO.cs` (15 files) | Request, query and response contracts (see [dtos-and-mapping.md](dtos-and-mapping.md)) |

## `ProjectOptimizerTests`

| File | Responsibility |
|---|---|
| `ProjectOptimizerTests.csproj` | xUnit project; copies `Fixtures/**/*.bpmn` to the output |
| `Fixtures/worked-example.bpmn` | The specification's worked example (golden values) |
| `Fixtures/Parsing/*.bpmn` | `bpmn2-prefix`, `default-namespace`, `bpmn-js-generated`, `xxe`, `billion-laughs` |
| `Fixtures/Validation/dv-01 … dv-17*.bpmn` | One fixture per diagram issue code |
| `Infrastructure/*.cs` | Test helpers (see [testing.md](testing.md#infrastructure)) |
| `Unit/Analysis/*Tests.cs` | Engine unit tests |
| `Unit/Service/*Tests.cs` | Service tests on in-memory SQLite |
| `Integration/Controller/*Tests.cs` | HTTP tests through `ApiWebApplicationFactory` |
