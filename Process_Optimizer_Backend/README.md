# Project Optimizer API

An ASP.NET Core 8 Web API that stores process diagrams for projects and analyses them. The frontend draws a BPMN
diagram, gives tasks a **time** and a **price**, and exports it as JSON (`states` and `branches`). The API turns that
export into a BPMN 2.0 diagram, checks it, and lists every **scenario**: one per combination of choices at
exclusive or inclusive gateways. For each scenario it computes the critical path, total duration and total cost.

There are no users and no authentication, so **do not expose this API to the internet**.

## What it does

1. **Projects:** create, list, read, update and delete. A project has a unique name, a time unit (Minutes, Hours,
   Days, Weeks) and a currency. Deleting a project also deletes its diagram.
2. **Diagram:** each project has exactly one diagram. Saving overwrites it; there is no history and the last save
   wins.
   - Save the frontend's JSON export (`PUT …/diagram/json`), or a raw BPMN XML (`PUT …/diagram`).
   - Read it back, or download it as a `.bpmn` file.
3. **Validation (internal, no endpoint):** every save is checked against 17 rules (DV-01 … DV-17) such as "no start
   event", "unreachable node" or "cycle". Issues never block saving; the diagram only carries its `errorCount`,
   `warningCount` and `isOptimizable`. The scenario endpoints answer 409 for a diagram with errors.
4. **Scenarios:** get the scenario count, then fetch the scenarios one by one by index or all at once (up to 1000).
   Each scenario includes the schedule of each activity (earliest/latest start and finish, slack), the critical path,
   the total duration and the total cost. Scenarios are computed on request and never stored.
5. **Resource limits:** the minimum and maximum cost and critical-path duration over all scenarios, using either
   the planned or the pessimistic estimates.

## The frontend JSON format

`PUT /api/projects/{projectId}/diagram/json` takes the frontend export as is:

```json
{
  "states": [
    { "id": "Start", "name": "Start", "type": "bpmn:StartEvent", "time": "", "price": "", "Depends_on": [], "Connects_to": ["Task_1"] },
    { "id": "Task_1", "name": "Pack", "type": "bpmn:Task", "time": "15m", "price": "25 EUR", "Depends_on": ["Start"], "Connects_to": ["End"] },
    { "id": "End", "name": "End", "type": "bpmn:EndEvent", "time": "", "price": "", "Depends_on": ["Task_1"], "Connects_to": [] }
  ],
  "branches": [
    { "id": "Flow_1", "name": "", "source": "Start", "target": "Task_1", "type": "sequence", "condition": null },
    { "id": "Flow_2", "name": "", "source": "Task_1", "target": "End", "type": "sequence", "condition": null }
  ]
}
```

- `type` of a state is a BPMN element type such as `bpmn:Task`, `bpmn:ExclusiveGateway` or `bpmn:EndEvent`.
- `time` and `price` are converted to the project's unit and currency. For example, `15m` becomes `0.010417` in a
  project that counts in Days, and `25 EUR` becomes `25` in a EUR project. A plain number is used as is. A value
  that cannot be converted (such as `25 USD` in a EUR project) is kept as typed and counted as a DV-09 error.
- A branch with `type: "nor"` becomes the default flow of its gateway. A state can have at most one.
- The connections are taken from `branches`. `Depends_on` and `Connects_to` are accepted but ignored (not stored).

The C# classes for this format are in `ProjectOptimizerLib/Model/Process/`: `ProcessDiagram`, `ProcessState`,
`ProcessBranch` and `ProcessBranchTypes`. Use `ProcessDiagram.Parse(json)` to read an export anywhere in the code,
and `diagram.ToJson()` to write one.

## Endpoints

All routes start with `/api`. Every JSON response uses the same envelope:
`{ "statusCode": 200, "message": "Success", "date": "…", "data": … }`. On errors there is no `data`, and validation errors come
with an `errors` object listing the problems for each field.

| Method | Route | Purpose |
|---|---|---|
| GET | `/projects` | List all projects, most recently updated first |
| POST | `/projects` | Create a project (starts with a default diagram) |
| GET | `/projects/{id}` | Read a project with its diagram summary |
| PUT | `/projects/{id}` | Replace name, description, time unit and currency |
| DELETE | `/projects/{id}` | Delete a project with everything in it |
| GET | `/projects/{projectId}/diagram` | The diagram, including the BPMN XML |
| PUT | `/projects/{projectId}/diagram/json` | Overwrite the diagram with the frontend JSON export |
| PUT | `/projects/{projectId}/diagram` | Overwrite the diagram with a BPMN XML |
| GET | `/projects/{projectId}/diagram/file` | Download the diagram as `.bpmn` |
| GET | `/projects/{projectId}/scenario-count` | Number of scenarios |
| GET | `/projects/{projectId}/scenarios` | All scenarios (409 above 1000 — then fetch them one by one) |
| GET | `/projects/{projectId}/scenarios/{index}` | One scenario with its schedule and critical path |
| GET | `/projects/{projectId}/resource-limits?basis=` | Cost and duration limits (`Planned` or `Pessimistic`) |

Scenario endpoints always use the project's current diagram.

Status codes:
- **400:** invalid input.
- **404:** unknown project or scenario index.
- **409:** a name conflict, a diagram that has errors or too many scenarios, or more than 1000 scenarios for `GET …/scenarios`.

## Further documentation

- [endpoint/](endpoint/README.md): one short page per controller with request and response examples.
- [Docs/EndUser/README.md](Docs/EndUser/README.md): for API consumers. Covers workflows, every endpoint, errors and
  the data reference.
- [Docs/Developer/README.md](Docs/Developer/README.md): for maintainers. Covers architecture, services, the analysis
  engine, the database, testing and how to extend the API.

## Run it

Prerequisite: the .NET SDK 8.0. SQLite is embedded, and `dotnet-ef` is a local tool.

```bash
dotnet tool restore
dotnet run --project ProjectOptimizerAPI
```

The API listens on `http://localhost:5212`. Swagger UI is at `http://localhost:5212/swagger` in Development.
`ProjectOptimizerAPI/ProjectOptimizerAPI.http` has a ready-made request for each endpoint, in the order of a typical
session. In Development, CORS allows the frontend at `http://localhost:5173` (and `http://localhost:3000`); other
origins go in `Cors:AllowedOrigins` in `appsettings.Development.json`.

## Database

- SQLite file `ProjectOptimizerAPI/projectoptimizer.db`, connection string `DefaultConnection`.
- Migrations are applied on startup. The database starts empty; there is no seed data.
- Tables: `Projects` and `Diagrams` (one row per project).
- The migrations were squashed into a single `InitialCreate`. A `projectoptimizer.db` created before that will not
  start; delete it (see below).
- To reset it, stop the API, delete `projectoptimizer.db` (and any `-shm` / `-wal` files), then start it again.
- To add a migration:

  ```bash
  dotnet ef migrations add <Name> --project ProjectOptimizerAPI --output-dir Migrations
  ```

## Tests

```bash
dotnet test ProjectOptimizer.sln
```

The tests include unit tests for the analysis engine and the services, and integration tests over HTTP with
`WebApplicationFactory`. All of them use in-memory SQLite.

## Project structure

```
ProjectOptimizerAPI/              web project
  Controllers/                    one controller per resource; thin, they only call a service
  Lib/Service/                    use cases: projects, diagrams, scenarios
  Lib/Analysis/                   the analysis engine (pure, no database)
    ProcessJsonConverter.cs       frontend JSON  -> BPMN XML (with a generated layout)
    BpmnGraphParser.cs            BPMN XML       -> ProcessGraph
    DiagramValidator.cs           ProcessGraph   -> issues (DV-01 … DV-17), used internally
    ProcessStructureAnalyzer.cs   ProcessGraph   -> tree of split/join fragments (scenario structure)
    ScenarioIndexer.cs            scenario index <-> gateway choices
    ScenarioBuilder.cs            choices        -> scenario (with CriticalPathCalculator)
    ResourceLimitCalculator.cs    min/max cost and duration over all scenarios
    DiagramAnalyzer.cs            ties the above together and caches results by content hash
  Lib/Repository/, Lib/Collection/UnitOfWork.cs   database access (EF Core)
  Lib/Database/                   DbContext, entity configuration, startup migration
  Lib/Mapping/                    Mapster mappings from entities to DTOs
  Lib/Middleware/, Lib/Exceptions/  turn exceptions into 400/404/409 responses
  Lib/Interface/                  interfaces of the services, repositories and analysis components
  Lib/Helper/                     small utilities (time/price text conversion, hashing, paging, ...)
  Migrations/                     EF Core migrations
ProjectOptimizerLib/              shared class library
  Model/                          database entities: Project, Diagram
  Model/Process/                  the frontend JSON format: ProcessDiagram, ProcessState, ProcessBranch
  DTO/                            request and response shapes
  Enum/                           enums used by the API
ProjectOptimizerTests/            xUnit: Unit/, Integration/, Infrastructure/ (test helpers), Fixtures/
```

### How a request flows

`Controller → Service → UnitOfWork / Repository → DbContext`. Scenario endpoints go through `ScenarioCatalog`, which
loads the project's diagram and asks `DiagramAnalyzer` for the cached analysis of its XML.

The diagram is always stored as BPMN XML, even when it arrives as JSON. That way there is only one analysis pipeline,
and a stored diagram can be opened again in any BPMN editor.

### Design notes

| Topic | Decision |
|---|---|
| Storage | One diagram per project, overwritten on every save; it keeps the exact XML, its SHA-256 hash, the issue counts and the planned cost/duration limits |
| Validation | Only malformed XML, a DTD, a non-BPMN root, more than 2 MB or more than 1000 nodes are rejected with 400; other issues are only counted on the diagram and never block saving |
| Scenario order | Every scenario has a fixed index and a key such as `G3=F`, so the same diagram always gives the same order |
| Numbers | `decimal` with invariant culture, so results are reproducible |
| Enums | Written as strings in JSON and in the database |
| Caching | Analyses and scenarios are cached in `IMemoryCache` by the diagram's content hash |
