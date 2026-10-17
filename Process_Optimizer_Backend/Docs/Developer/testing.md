# Testing

## Layout

```
ProjectOptimizerTests/
├── Fixtures/                     .bpmn files copied to the output directory
│   ├── worked-example.bpmn
│   ├── Parsing/                  bpmn2-prefix, default-namespace, bpmn-js-generated, xxe, billion-laughs
│   └── Validation/               dv-01 … dv-17 (one fixture per diagram issue code)
├── Infrastructure/               shared test helpers
├── Unit/Analysis/                analysis engine, no database
├── Unit/Service/                 services + repositories on in-memory SQLite
└── Integration/Controller/       full HTTP pipeline through WebApplicationFactory
```

There are 394 test methods (332 unit, 62 integration; 370 `[Fact]`, 24 `[Theory]`), which run as 537 test cases
(last run 2026-10-12: 537 passed).

Test names follow `Method_Scenario_ExpectedResult`. Namespaces follow the folders, for example
`ProjectOptimizerTests.Unit.Service`.

## Infrastructure

| Class | Purpose |
|---|---|
| `SqliteTestDatabase` | Opens a private `Data Source=:memory:` connection, keeps it open, and creates the schema with `EnsureCreated()` (test-only; the application uses migrations). `CreateDbContext()` returns a context on that connection. |
| `ServiceTestBase` | Base class for service tests. Builds a real `UnitOfWork` with the two repositories, `ProjectAccessGuard`, `DiagramResolver`, `ScenarioCatalog`, a `FakeTimeProvider` fixed at `2026-01-15T09:00:00Z`, `AnalysisTestComponents` and a `TestDataBuilder`, plus `LoadProjectAsync(id)` / `LoadDiagramAsync(projectId)` (no-tracking reads for assertions). Scans the Mapster configs once. `Dispose` closes the context and the database. |
| `AnalysisTestComponents` | Wires every real analysis component (parser, structure analyzer, validator, indexer, CPM, builder, limits, a size-limited `MemoryCache`, analyzer, `DiagramFactory`) without DI. `Analyze(xml)` hashes the XML and analyses it. |
| `TestDataBuilder` | Inserts test rows directly, using the real `DiagramFactory`: `AddProjectAsync(name?, bpmnXml?, timeUnit, currency)` (its diagram is the given XML or the default diagram), `SetDiagramAsync(project, xml)` (overwrites the diagram). |
| `BpmnDiagramBuilder` | Fluent builder for BPMN XML (`StartEvent`, `EndEvent`, `Task`, `Gateway`, `Node`, `Flow`, `Raw`, `Build`), plus generators `SequentialExclusiveChoices(n)` (2^n scenarios) and `Chain(n)`. |
| `BpmnFixtures` | Loads fixture files (`WorkedExample`, `Load(path)`), plus the small diagrams `DisconnectedDiagram()` and `InclusiveSplitDiagram()`. |
| `ProcessJsonFixtures` | Builds frontend exports from `ProcessState` / `ProcessBranch` (`State`, `Branch`, `Diagram`), plus the sample export (`SampleStates`, `SampleBranches`) and an exclusive-choice export (`ExclusiveChoiceStates`, `ExclusiveChoiceBranches`). |
| `RandomDiagramGenerator` | `Generate(seed)` produces random well-structured diagrams for the resource-limit cross-check in `ResourceLimitCalculatorTests`. |
| `ApiWebApplicationFactory` | `WebApplicationFactory<Program>` in `Development`. It replaces the DbContext options with a shared in-memory SQLite connection. Startup still runs the migration, so each class starts with an empty database and creates its own projects. |
| `HttpClientJsonExtensions` | `ReadApiResponseAsync<T>`, `ReadApiErrorAsync`, `PostAndReadDataAsync<T>`, and the shortcuts `CreateProjectAsync`, `CreateProjectWithDiagramAsync`, `SaveDiagramAsync` (a `PUT …/diagram` that returns the `DiagramDTO`). |
| `ApiResponseAssertions` | `AssertApiErrorAsync(status, message?)`, `AssertApiErrorContainingAsync`, `AssertValidationErrorAsync(field)`. |
| `ScenarioAssertions` | `AssertReference(ref, index, key)` for `ScenarioRefDTO`. |

## How to run

From the repository root:

```bash
dotnet restore
dotnet build ProjectOptimizer.sln -warnaserror
dotnet test ProjectOptimizer.sln
dotnet test ProjectOptimizer.sln --filter "FullyQualifiedName~ProjectOptimizerTests.Unit.Analysis"
dotnet test ProjectOptimizer.sln --filter "FullyQualifiedName~ScenarioControllerTests"
dotnet test ProjectOptimizer.sln --collect:"XPlat Code Coverage"
```

The tests need no external services and use in-memory SQLite databases:

- **Service tests** get a fresh database per test, because xUnit creates a new `ServiceTestBase` instance for each
  test.
- **Integration test classes** share one `ApiWebApplicationFactory` (and its database) per class through
  `IClassFixture<ApiWebApplicationFactory>`.

## What each test class covers

### Unit/Analysis

| Class | Covers |
|---|---|
| `BpmnGraphParserTests` | Worked-example graph; namespace prefixes and default namespace; culture-independent decimals; every supported element kind; XXE, billion laughs, DTD, malformed XML, non-BPMN root (BR-08); 2 MB and 1000-node limits incl. boundaries (BR-17); DV-01, 04, 05, 09, 16; default flows, conditions, names, duplicate ids, event-based gateways |
| `DiagramValidatorTests` | DV-02 … DV-17 one by one, gating of DV-06 / DV-07 / DV-12, warnings-only stays optimizable, deterministic issue order, one fixture per DV code |
| `ProcessStructureAnalyzerTests` | Worked-example tree, join matching, empty branches, several start/end events, nested counts, inclusive counts, counts beyond `long`, DV-12 cases |
| `ScenarioIndexerTests` | Unrank order (exclusive, inclusive, nested, sequences), gateway kind and controllability, determinism, key formatting |
| `ScenarioBuilderTests` | Golden scenarios 0 and 1 (21 d / 23 000 and 20 d / 25 000), sequential duration, slack of `C`, element / flow ids, critical path, path count, activity selection, flags, determinism |
| `CriticalPathCalculatorTests` | Chains, parallel branches, slack, several ends, ties, duplicate edges, empty input, normalisation |
| `ResourceLimitCalculatorTests` | Golden limits and references, pessimistic basis, inclusive rules, tie-breaking, joint worst case, exclusive durations, notes, index limit, random cross-check |
| `DiagramAnalyzerTests` | Cache hits per hash and per index, worked-example analysis, default diagram without structure, malformed XML, invalid-operation guards, 1000-node performance |
| `DiagramFactoryTests` | `Create` and `Apply`: summary and planned limits, hash and size, `UpdatedAt`, null limits for non-optimizable diagrams, BR-09; a rejected XML leaves the diagram unchanged |
| `MetricTextConverterTests` | Blank, plain numbers, every time unit and combined units, rounding, matching currency codes and symbols, other currencies and unreadable text kept |
| `ProcessJsonConverterTests` | Frontend sample export, unit conversion per project unit, original texts, default flows and conditions, DI for every node and branch, layout, cycles, artifacts, DV-04 / DV-05 / DV-09 passthrough, generated-id collisions, invalid / duplicate ids, invalid types, two default branches |
| `ProcessDiagramTests` | `ProcessDiagram.Parse` of a frontend export, `ToJson` round trip with the frontend property names (`Depends_on`, `Connects_to`), `ProcessBranch.IsDefault` only for `nor` |
| `TopologicalOrderTests` | Priority order, cycles, duplicate and unknown edges |

### Unit/Service

| Class | Covers |
|---|---|
| `ProjectServiceTests` | Create (defaults, trimming, BR-04, BR-05), list (`UpdatedAt` descending, stored limits of the diagram), get, update (BR-15, BR-23, own name), delete (cascade) |
| `DiagramServiceTests` | Get, overwrite (BR-08, BR-09, BR-15, BR-17), JSON save (conversion with project units, 400, 404), file download name sanitising |
| `ScenarioServiceTests` | Count (BR-11, BR-12 at 52/53/54 binary choices), get (BR-30), all scenarios (index order, equal to the single scenarios, more than 1000 scenarios, BR-11, BR-12), resource limits (golden values, basis, above-index-limit) |
| `ScenarioCatalogTests` | Optimizable / indexed diagram guards, unknown project |
| `DiagramResolverTests` | Resolved XML, hash and project context; unknown project |
| `ProjectAccessGuardTests` | IR-01 guard |

### Integration/Controller

| Class | Covers |
|---|---|
| `ProjectControllerTests` | Every project endpoint with envelope, `Location`, 400 / 404 / 409 cases |
| `DiagramJsonControllerTests` | JSON save of a frontend export (200, generated XML), scenario endpoints after a JSON save, field validation, duplicate id, unknown project |
| `DiagramControllerTests` | Every diagram endpoint, overwrite on save, XXE / billion-laughs / oversized bodies, 3 MiB request limit, XML attachment |
| `ScenarioControllerTests` | Count, all scenarios (incl. 409 above 1000), single scenario, resource limits incl. negative index, invalid basis and 409 cases |
