# DTOs and mapping

All contract types are in `ProjectOptimizerLib/DTO` (namespace `ProjectOptimizerLib.DTO`), the frontend JSON format
is in `ProjectOptimizerLib/Model/Process` (see [Frontend JSON format](#frontend-json-format)), and the enums are in
`ProjectOptimizerLib/Enum`. JSON uses camelCase. Property names are case-insensitive on input, and enums are
strings (`JsonStringEnumConverter`). Request DTOs use `set` accessors and nullable value types so that `[Required]`
can detect a missing value. Response DTOs use `init` accessors.

Shared limits are in `FieldLimits`: `NameMaxLength = 100`, `TextMaxLength = 1000`,
`BpmnIdMaxLength = 255`, `BpmnTypeMaxLength = 100`, `MetricTextMaxLength = 100`.

`TrimmedStringLengthAttribute(maximumLength)` passes when the value is not a string, or when
`value.Trim().Length ≤ maximumLength`.

## Envelope

| Type | Fields | Used by |
|---|---|---|
| `ApiResponse` | `statusCode` int, `message` string, `date` DateTime (UTC, default `DateTime.UtcNow`), `errors` `IDictionary<string,string[]>?` | every error response |
| `ApiResponse<TData>` | + `data` `TData?` | every JSON success response |

## Request and query DTOs

### ProjectCreateDTO — body of `POST /api/projects`

| Field | Type | Validation (message) | Default |
|---|---|---|---|
| `name` | string | `[Required]` (`The project name is required.`) · `[TrimmedStringLength(100)]` (`The project name must be at most 100 characters long.`) | – |
| `description` | string? | `[StringLength(1000)]` (`The description must be at most 1000 characters long.`) | `null` |
| `timeUnit` | `ETimeUnit?` | `[EnumDataType]` (`The time unit must be Minutes, Hours, Days or Weeks.`) | `Days` (service) |
| `currency` | string? | `[RegularExpression("^[A-Z]{3}$")]` (`The currency must be a three-letter upper-case ISO 4217 code.`) | `EUR` (service) |

### ProjectUpdateDTO — body of `PUT /api/projects/{id}`

This DTO replaces all settings. It has the same fields and messages as `ProjectCreateDTO`, plus
`[Required(ErrorMessage = "The time unit is required.")]` on `timeUnit` and
`[Required(ErrorMessage = "The currency is required.")]` on `currency`. A `null` `description` clears it. The class
carries an XML summary saying that changing `TimeUnit` or `Currency` does not convert the numbers in the diagram
(BR-23).

### DiagramSaveDTO — body of `PUT /api/projects/{projectId}/diagram`

| Field | Type | Validation (message) |
|---|---|---|
| `bpmnXml` | string | `[Required]` (`The diagram XML is required.`) |

### Body of `PUT /api/projects/{projectId}/diagram/json`

There is no wrapper DTO: the action binds the frontend export `ProcessDiagram` (`states`, `branches`) directly. A
missing list stays empty; the elements are validated recursively. See [Frontend JSON format](#frontend-json-format).

### ResourceLimitsQueryDTO — query of `GET /api/projects/{projectId}/resource-limits`

| Field | Type | Validation (message) | Default |
|---|---|---|---|
| `basis` | `EResourceLimitBasis?` | `[EnumDataType]` (`The basis must be Planned or Pessimistic.`) | `Planned` |

## Response DTOs

### Projects

| DTO | Fields | Used by |
|---|---|---|
| `ProjectListItemDTO` | `id`, `name`, `timeUnit`, `currency`, `isOptimizable`, `maxCost` decimal?, `maxDurationBound` decimal?, `createdAt`, `updatedAt` | `GET /api/projects` (as `List<ProjectListItemDTO>`, most recently updated first) |
| `ProjectDetailsDTO` | `id`, `name`, `description`, `timeUnit`, `currency`, `createdAt`, `updatedAt`, `maxCost` decimal?, `maxDurationBound` decimal?, `diagram` (`DiagramSummaryDTO`) | `POST /api/projects`, `GET /api/projects/{id}` |

### Diagram

| DTO | Fields | Used by |
|---|---|---|
| `DiagramDTO` | `updatedAt`, `bpmnXml`, `contentSha256`, `sizeBytes`, `flowNodeCount`, `errorCount`, `warningCount`, `isOptimizable` | `GET …/diagram`, `PUT …/diagram`, `PUT …/diagram/json` |
| `DiagramSummaryDTO` | the same fields without `bpmnXml` and `contentSha256` | `ProjectDetailsDTO.diagram` |
| `DiagramFileDTO` | `fileName`, `content` | internal: service → controller for `GET …/diagram/file` (never serialised) |

### Scenarios

| DTO | Fields | Used by |
|---|---|---|
| `ScenarioCountDTO` | `orderingVersion`, `count` (long) | `GET …/scenario-count` |
| `ScenarioDTO` | `projectId`, `orderingVersion`, `index`, `count`, `key`, `choices`, `elementIds`, `sequenceFlowIds`, `activities`, `criticalPath`, `pathCount`, `totalDuration`, `sequentialDuration`, `totalCost`, `hasExclusiveActivities`, `hasMissingEstimates`, `timeUnit`, `currency` | `GET …/scenarios/{index}`, `GET …/scenarios` (as `List<ScenarioDTO>`, index order) |
| `ScenarioChoiceDTO` | `gatewayId`, `gatewayKind` (`EFlowNodeKind`), `isControllable`, `chosenTargetIds` | `choices` |
| `ScenarioActivityDTO` | `bpmnId`, `name`, `kind`, `duration`, `cost`, `parallelizable`, `earliestStart`, `earliestFinish`, `latestStart`, `latestFinish`, `slack`, `isCritical` | `ScenarioDTO.activities` |
| `ResourceLimitsDTO` | `projectId`, `basis`, `timeUnit`, `currency`, `scenarioCount` (long?), `maxCost`, `maxCostScenario`, `minCost`, `minCostScenario`, `maxCriticalPathDuration`, `maxCriticalPathScenario`, `minCriticalPathDuration`, `minCriticalPathScenario`, `maxExclusiveDuration`, `maxDurationBound`, `maxSequentialDuration`, `maxSequentialScenario`, `isJointWorstCase`, `hasExclusiveActivities`, `hasMissingEstimates`, `notes` | `GET …/resource-limits` |
| `ScenarioRefDTO` | `index`, `key` | the `*Scenario` fields of `ResourceLimitsDTO` (`null` above 2^53 − 1 scenarios) |

## Enums

All enums are serialised and accepted as strings. `ETimeUnit` is also stored as a string.

| Enum | Values | Meaning |
|---|---|---|
| `ETimeUnit` | `Minutes`, `Hours`, `Days`, `Weeks` | Unit of all durations of a project (a label only; values are never converted) |
| `EResourceLimitBasis` | `Planned`, `Pessimistic` | Duration basis for resource limits |
| `EIssueSeverity` | `Error`, `Warning` | Validation issue severity (internal); any `Error` makes the diagram not optimizable |
| `EFlowNodeKind` | `StartEvent`, `EndEvent`, `Task`, `SubProcess`, `CallActivity`, `IntermediateEvent`, `ExclusiveGateway`, `ParallelGateway`, `InclusiveGateway`, `EventBasedGateway` | Normalised BPMN node type |
| `EGatewayDirection` | `None`, `Split`, `Join` | Gateway role derived from flow counts (internal; never in a DTO) |
| `EFragmentKind` | `Leaf`, `Sequence`, `Parallel`, `Exclusive`, `Inclusive` | Structure-tree node kind (internal; never in a DTO) |

## Mapster configuration

`AddMappings()` scans the API assembly for `IRegister` classes and calls `services.AddMapster()`. Properties with
the same name are mapped by convention. The configs only declare what differs.

| Config | Mapping | Special rules |
|---|---|---|
| `ProjectMappingConfig` | `Project` → `ProjectListItemDTO` | `isOptimizable`, `maxCost` and `maxDurationBound` come from `source.Diagram.*`. The navigation translates to a SQL join inside `ProjectToType`. |
| | `Project` → `ProjectDetailsDTO` | `maxCost` and `maxDurationBound` from `source.Diagram.*`; `diagram` (`DiagramSummaryDTO`) maps from `Project.Diagram` by convention |

`Diagram` → `DiagramDTO` / `DiagramSummaryDTO` needs no config class; Mapster maps it by convention.

Some mappings are hand-written instead of using Mapster:

- `ScenarioBuilder.ToDto` (`ScenarioDTO`).
- `ResourceLimitCalculator` (`ResourceLimitsDTO`).
- `ScenarioService.GetScenarioCountAsync` (`ScenarioCountDTO`).
- `DiagramService.GetDiagramFileAsync` (`DiagramFileDTO`).

## Frontend JSON format

`ProjectOptimizerLib/Model/Process` (namespace `ProjectOptimizerLib.Model.Process`) holds the frontend's JSON export.
The property names are fixed with `[JsonPropertyName]`, so they do not depend on the serializer's naming policy. `ProcessDiagram`
is the request body of `PUT …/diagram/json`, so the Data Annotations of its elements run as model validation (400
with an `errors` dictionary).

| Type | Members | Notes |
|---|---|---|
| `ProcessDiagram` | `States` (`"states"`, `List<ProcessState>`), `Branches` (`"branches"`, `List<ProcessBranch>`) | `static Parse(string json)` deserialises with `JsonSerializerDefaults.Web` and throws `JsonException("The process diagram JSON is empty.")` for `null`; `ToJson()` serialises with the same options |
| `ProcessState` | `Id` (`[Required]`, ≤ 255), `Name` (≤ 1000), `Type` (`[Required]`, ≤ 100, such as `bpmn:Task`), `Time`, `Price` (≤ 100 each, texts such as `15m` or `25 EUR`), `DependsOn` (`"Depends_on"`), `ConnectsTo` (`"Connects_to"`) | `DependsOn` / `ConnectsTo` are bound but not used by the converter; the connections come from the branches |
| `ProcessBranch` | `Id`, `Source`, `Target` (`[Required]`, ≤ 255), `Name`, `Condition` (≤ 1000), `Type` (≤ 100) | `IsDefault` (`[JsonIgnore]`) is true when `Type`, trimmed and case-insensitive, equals `nor` |
| `ProcessBranchTypes` | `Sequence = "sequence"`, `Xor = "xor"`, `Nor = "nor"`, `And = "and"`, `Or = "or"`, `Complex = "complex"` | Only `nor` changes the generated XML (the gateway's default flow); other types are kept as sent and ignored |

Each length limit has its own message, for example `The state id must be at most 255 characters long.` and
`The branch source is required.`.
