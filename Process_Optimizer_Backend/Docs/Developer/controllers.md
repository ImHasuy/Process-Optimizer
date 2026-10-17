# Controllers

Every controller derives from `ApiControllerBase` (`[ApiController]`, `[Produces("application/json")]`). It depends
on exactly one service interface and contains one expression or two statements per action. Every action has XML
documentation and one `[ProducesResponseType]` per status code, so Swagger shows the same table as below.

Common behaviour:

- **400 from model validation.** This applies to any action with a body or query DTO, and to `GetScenario` (the
  `[Range]` on `index`). The response is produced automatically by `InvalidModelStateResponseFactory`.
- **Route constraints.** A route value that does not match `guid` or `long` gives an empty 404 from routing.
- **No authentication.** No action carries `[Authorize]`, so no action returns 401 or 403.

`ApiControllerBase` helpers:

| Helper | Result |
|---|---|
| `Success<TData>(TData data)` | `200 OK` with `ApiResponse<TData>` (`message = "Success"`) |
| `Created<TData>(string actionName, object routeValues, TData data)` | `201 Created` via `CreatedAtAction`, with `ApiResponse<TData>` (`statusCode = 201`) and a `Location` header |

## ProjectController

Route prefix `api/projects` · depends on `IProjectService`.

| Action | Route | Calls | Status codes |
|---|---|---|---|
| `ListProjects()` | `GET /api/projects` | `ListProjectsAsync()` | 200 `List<ProjectListItemDTO>` (most recently updated first) |
| `CreateProject([FromBody] ProjectCreateDTO request)` | `POST /api/projects` | `CreateProjectAsync(request)` | 201 `ProjectDetailsDTO`, `Location` → `GetProject` · 400 · 409 |
| `GetProject(Guid id)` | `GET /api/projects/{id}` (`{id:guid}`) | `GetProjectAsync(id)` | 200 `ProjectDetailsDTO` · 404 |
| `UpdateProject(Guid id, [FromBody] ProjectUpdateDTO request)` | `PUT /api/projects/{id}` | `UpdateProjectAsync(id, request)` | 204 · 400 · 404 · 409 |
| `DeleteProject(Guid id)` | `DELETE /api/projects/{id}` | `DeleteProjectAsync(id)` | 204 · 404 |

## DiagramController

Route prefix `api/projects/{projectId:guid}/diagram` · depends on `IDiagramService`.

| Action | Route | Calls | Status codes |
|---|---|---|---|
| `GetDiagram(Guid projectId)` | `GET /api/projects/{projectId}/diagram` | `GetDiagramAsync(projectId)` | 200 `DiagramDTO` · 404 |
| `SaveDiagram(Guid projectId, [FromBody] DiagramSaveDTO request)` | `PUT /api/projects/{projectId}/diagram` | `SaveDiagramAsync(projectId, request)` | 200 `DiagramDTO` · 400 · 404 · 413 |
| `SaveJsonDiagram(Guid projectId, [FromBody] ProcessDiagram request)` | `PUT /api/projects/{projectId}/diagram/json` | `SaveJsonDiagramAsync(projectId, request)` | 200 `DiagramDTO` · 400 · 404 · 413 |
| `DownloadDiagram(Guid projectId)` | `GET /api/projects/{projectId}/diagram/file` | `GetDiagramFileAsync(projectId)` | 200 `application/xml` file · 404 (JSON envelope) |

Notes:

- Both save actions overwrite the project's single diagram and answer 200 (not 201): no new resource is created. The
  JSON save binds the frontend export (`ProcessDiagram`) directly as the body.
- `SaveDiagram` and `SaveJsonDiagram` have `[RequestSizeLimit(AnalysisLimits.MaxSaveRequestBytes)]` (3 × 1024 × 1024
  bytes). A larger body
  makes Kestrel throw `BadHttpRequestException`, which `ApiExceptionHandler` maps to 413
  `"The request body is too large."`.
- `DownloadDiagram` returns `File(Encoding.UTF8.GetBytes(file.Content), "application/xml", file.FileName)`. The
  `Content-Disposition` header is `attachment; filename="<project name>.bpmn"`, and the body has no envelope.

## ScenarioController

Route prefix `api/projects/{projectId:guid}` · depends on `IScenarioService`.

| Action | Route | Calls | Status codes |
|---|---|---|---|
| `GetScenarioCount(Guid projectId)` | `GET /api/projects/{projectId}/scenario-count` | `GetScenarioCountAsync(projectId)` | 200 `ScenarioCountDTO` · 404 · 409 |
| `GetAllScenarios(Guid projectId)` | `GET /api/projects/{projectId}/scenarios` | `GetAllScenariosAsync(projectId)` | 200 `List<ScenarioDTO>` (index order) · 404 · 409 |
| `GetScenario(Guid projectId, [Range(0, long.MaxValue)] long index)` | `GET /api/projects/{projectId}/scenarios/{index}` (`{index:long}`) | `GetScenarioAsync(projectId, index)` | 200 `ScenarioDTO` · 400 · 404 · 409 |
| `GetResourceLimits(Guid projectId, [FromQuery] ResourceLimitsQueryDTO query)` | `GET /api/projects/{projectId}/resource-limits?basis=` | `GetResourceLimitsAsync(projectId, query)` | 200 `ResourceLimitsDTO` · 400 · 404 · 409 |

The `[Range]` on `index` is declared as
`[Range(typeof(long), "0", "9223372036854775807", ErrorMessage = "The scenario index must be zero or greater.")]`.
Without it, `{index:long}` would accept negative values.

There is no endpoint for the parsed graph or the validation issues. A diagram with errors answers 409
`"The diagram has errors and cannot be analysed."` on all four scenario endpoints; `scenario-count`, `scenarios` and
`scenarios/{index}` also answer 409 when the scenario count is too large to index. `scenarios` takes no query
parameters and answers 409 `"The diagram has more than 1000 scenarios; request them one by one."` when the count is
above `AnalysisLimits.MaxListedScenarios`.

## Endpoint inventory (13 operations)

| # | Method | Path | Controller |
|---|---|---|---|
| 1 | GET | `/api/projects` | Project |
| 2 | POST | `/api/projects` | Project |
| 3 | GET | `/api/projects/{id}` | Project |
| 4 | PUT | `/api/projects/{id}` | Project |
| 5 | DELETE | `/api/projects/{id}` | Project |
| 6 | GET | `/api/projects/{projectId}/diagram` | Diagram |
| 7 | PUT | `/api/projects/{projectId}/diagram` | Diagram |
| 8 | PUT | `/api/projects/{projectId}/diagram/json` | Diagram |
| 9 | GET | `/api/projects/{projectId}/diagram/file` | Diagram |
| 10 | GET | `/api/projects/{projectId}/scenario-count` | Scenario |
| 11 | GET | `/api/projects/{projectId}/scenarios` | Scenario |
| 12 | GET | `/api/projects/{projectId}/scenarios/{index}` | Scenario |
| 13 | GET | `/api/projects/{projectId}/resource-limits` | Scenario |
