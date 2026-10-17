# Validation and errors

## Formal vs. business validation

| Kind | Where | When it runs | Result |
|---|---|---|---|
| **Formal** | Data Annotations on request/query DTOs, `[Range]` on the `index` route parameter, JSON and enum binding, route constraints | Before the action (`[ApiController]` automatic model validation) | 400 envelope with `message = "One or more validation errors occurred."` and `errors` keyed by field |
| **Business** | `Ensure…` guards in services and helpers; hard rejections in `BpmnGraphParser` and `ProcessJsonConverter` | Inside the service call | `ApiException` subclass → 400 / 404 / 409 envelope with `errors = null` |
| **Diagram issues** | `BpmnGraphParser`, `DiagramValidator`, `ProcessStructureAnalyzer` | When a diagram is saved or analysed | **No HTTP error.** The issues stay internal; only `errorCount`, `warningCount` and `isOptimizable` are stored on the diagram. Any `Error` issue makes the scenario endpoints answer 409 (BR-11). |

`ServiceCollection.AddApiControllers` contains these settings:

- `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true`, so only explicit `[Required]` attributes
  apply.
- `InvalidModelStateResponseFactory` returns `ApiResponseFactory.ValidationError(ModelState)`.

The `errors` keys are the model-state keys:

- **Validation attributes** use the property name in PascalCase, for example `"Name"` or `"Currency"`.
- **JSON conversion failures** use a JSON path, for example `"$.timeUnit"` for an unknown enum name, with the
  framework's conversion message.

```json
{ "statusCode": 400, "message": "One or more validation errors occurred.", "date": "2026-10-10T19:15:14.7663208Z",
  "errors": { "Name": ["The project name is required."], "Currency": ["The currency must be a three-letter upper-case ISO 4217 code."] } }
```

## Exception types → status codes

| Exception | Status | Thrown by |
|---|---|---|
| `InvalidRequestException` | 400 | Parser hard rejections, `ProcessJsonConverter` (invalid or duplicate id, invalid state type, several default branches of one state) |
| `NotFoundException` | 404 | `Repository<T>.GetRequiredAsync`, `ProjectAccessGuard`, `DiagramService.EnsureFound` and `DiagramResolver` (`The project has no diagram.`), `ScenarioService.EnsureScenarioExists` |
| `ConflictException` | 409 | Project name guard, `ScenarioCatalog` guards, `ScenarioService.GetAllScenariosAsync` (more than 1000 scenarios), `UnitOfWork.SaveChangesOrConflictAsync` |
| `BadHttpRequestException` (framework) | its own code (413 for an oversized body) | Kestrel / model binding |
| anything else | 500 `"An unexpected error occurred."` | unexpected failures, including the internal `InvalidOperationException` safety checks in `DiagramAnalyzer` and `ScenarioBuilder` |

## ApiExceptionHandler

`Lib/Middleware/ApiExceptionHandler.cs` implements `IExceptionHandler` and handles every exception:

1. `CreateResponse(exception)` picks the envelope:
   - `ApiException` → `ApiResponseFactory.Error(statusCode, message)`.
   - `BadHttpRequestException` → its status code, with `"The request body is too large."` for 413 and
     `"The request is malformed."` otherwise.
   - Anything else → 500 `"An unexpected error occurred."`.
2. `LogException` logs 5xx results as errors with the exception, and 4xx results as warnings with the status code
   and message only. The diagram XML is never logged.
3. It writes the status code and the JSON envelope, and returns `true`.

## Rule list

The last column names test methods or test classes in `ProjectOptimizerTests`
([testing.md](testing.md#what-each-test-class-covers)).

### Business rules

| Rule | Text | Guard (code) | Status | Message | Tests (examples) |
|---|---|---|---|---|---|
| BR-04 | Project names are unique globally (trimmed, case-insensitive) on create and update | `ProjectService.EnsureNameIsAvailableAsync` + unique index via `SaveChangesOrConflictAsync` | 409 | `A project with this name already exists.` | `CreateProjectAsync_DuplicateNameInOtherCasing_ThrowsConflict`, `UpdateProjectAsync_NameOfOtherProject_ThrowsConflict`, `CreateProject_DuplicateName_ReturnsConflict` |
| BR-05 | A new project gets the default diagram, verbatim | `ProjectService.CreateProjectAsync` → `diagramFactory.Create(project.Id, DefaultDiagram.Xml, project.CreatedAt)` | – | – | `ProjectServiceTests`, `DiagramControllerTests` |
| BR-08 | Malformed XML, any DTD, or a non-BPMN root is rejected | `BpmnGraphParser.LoadDocument`, `EnsureBpmnRoot` | 400 | `The diagram is not well-formed XML or contains a DTD.` / `The diagram root element must be a BPMN 2.0 definitions element.` | `Parse_XxePayload_ThrowsInvalidRequest`, `Parse_BillionLaughsPayload_ThrowsInvalidRequest`, `Parse_NonBpmnRoot_ThrowsInvalidRequest`, `SaveDiagram_XxePayload_ReturnsBadRequest` |
| BR-09 | Semantic diagram problems never block saving | `DiagramFactory.Create` / `Apply` (store counts, never throw for DV issues) | – | – | `DiagramFactoryTests`, `DiagramServiceTests` |
| BR-11 | Scenario requests on a diagram with any `Error` issue are rejected | `ScenarioCatalog.EnsureOptimizable` | 409 | `The diagram has errors and cannot be analysed.` | `GetScenarioCountAsync_DiagramWithErrors_ThrowsConflict`, `GetResourceLimitsAsync_DiagramWithErrors_ThrowsConflict`, `GetScenarioCount_NewProject_ReturnsConflict` |
| BR-12 | More than 2^53 − 1 scenarios → 409 for indexed requests; resource limits are still served without references | `ScenarioCatalog.EnsureIndexable` | 409 | `The diagram has too many scenarios to index.` | `GetScenarioCountAsync_FiftyFourBinaryChoices_ThrowsConflict`, `GetResourceLimitsAsync_FiftyFourBinaryChoices_ReturnsLimitsWithoutReferences` |
| BR-13 | Only diagrams with exactly one process can be analysed (DV-01 → BR-11) | `BpmnGraphParser` (DV-01) | 409 via BR-11 | BR-11 message | `Parse_TwoProcesses_ReportsDv01` |
| BR-14 | Only acyclic diagrams can be analysed (DV-08 → BR-11) | `DiagramValidator.CheckAcyclic` | 409 via BR-11 | BR-11 message | `Validate_Cycle_ReportsDv08ForEveryNodeOfTheCycle` |
| BR-15 | `Project.UpdatedAt` changes on every project update and every diagram save | `ProjectService.UpdateProjectAsync`, `DiagramService.OverwriteAsync` | – | – | `UpdateProjectAsync_ValidRequest_ReplacesFieldsAndUpdatesTimestamp`, `DiagramServiceTests` |
| BR-16 | Deleting a project hard-deletes its diagram | `DiagramConfiguration` (cascade), `ProjectService.DeleteProjectAsync` | – | – | `ProjectServiceTests` |
| BR-17 | XML > 2 MB (UTF-8, before parsing) or > 1000 flow nodes is rejected | `BpmnGraphParser.EnsureWithinSizeLimit`, `EnsureWithinFlowNodeLimit` | 400 | `The diagram XML must not be larger than 2 MB.` / `The diagram must not contain more than 1000 flow nodes.` | `Parse_XmlLargerThanTwoMegabytes_ThrowsInvalidRequest`, `Parse_MoreThanThousandFlowNodes_ThrowsInvalidRequest`, `SaveDiagram_OversizedXml_ReturnsBadRequest` |
| BR-23 | Changing `TimeUnit` or `Currency` does not convert diagram numbers | `ProjectService.UpdateProjectAsync` (diagram untouched) + XML docs | – | – | `ProjectServiceTests` |
| BR-30 | A route scenario index ≥ count answers 404 | `ScenarioService.EnsureScenarioExists` | 404 | `The scenario does not exist for this diagram.` | `GetScenarioAsync_IndexEqualToCount_ThrowsNotFound`, `GetScenario_IndexTwo_ReturnsNotFound` |

### Implementation rules (IR)

| Rule | Text | Guard (code) | Status | Message | Tests (examples) |
|---|---|---|---|---|---|
| IR-01 | Every project-scoped request targets an existing project | `ProjectAccessGuard.GetProjectAsync` / `EnsureExistsAsync` (via `Repository<Project>.GetRequiredAsync`) | 404 | `Project with id '<id>' was not found.` | `GetProjectAsync_GuardUnknownProject_ThrowsNotFound`, `GetProject_UnknownId_ReturnsNotFound`, `SaveDiagram_UnknownProject_ReturnsNotFound`, `GetAllScenarios_UnknownProject_ReturnsNotFound` |

### Formal validation rules (VAL)

| Rule | Where | Status | Tests (examples) |
|---|---|---|---|
| VAL-10 | `ProjectCreateDTO` / `ProjectUpdateDTO.Name`: `[Required]` + `[TrimmedStringLength(100)]`; the service trims | 400 | `CreateProject_MissingName_ReturnsBadRequestWithNameError`, `CreateProject_WhitespaceName_ReturnsBadRequest`, `CreateProject_HundredCharacterNameWithSurroundingSpaces_CreatesTrimmedName` |
| VAL-11 | `Description`: `[StringLength(1000)]` | 400 | `CreateProject_DescriptionTooLong_ReturnsBadRequest` |
| VAL-12 | `TimeUnit`: `[EnumDataType]` (+ `[Required]` on update); default `Days` | 400 | `CreateProjectAsync_OmittedTimeUnitAndCurrency_DefaultsToDaysAndEur`, `UpdateProject_MissingTimeUnit_ReturnsBadRequest`, `CreateProject_UnknownTimeUnit_ReturnsBadRequest` |
| VAL-13 | `Currency`: `^[A-Z]{3}$` (+ `[Required]` on update); default `EUR` | 400 | `CreateProject_LowercaseCurrency_ReturnsBadRequest`, `UpdateProject_MissingCurrency_ReturnsBadRequest` |
| VAL-20 | The parser resolves elements by namespace URI (any prefix) and reads numbers with the invariant culture | – | `Parse_Bpmn2Prefix_ProducesSameGraphAsBpmnPrefix`, `Parse_DefaultNamespace_ProducesSameGraphAsBpmnPrefix`, `Parse_UnderCommaDecimalCulture_ReadsDotDecimal` |
| VAL-21 | `DiagramSaveDTO` annotations and those on each `ProcessState` / `ProcessBranch` of the `ProcessDiagram` body | 400 | `DiagramControllerTests`, `DiagramJsonControllerTests` |
| VAL-60 | `ResourceLimitsQueryDTO` annotations + `[Range]` on the route `index` | 400 | `GetScenario_NegativeIndex_ReturnsBadRequest`, `GetResourceLimits_InvalidBasis_ReturnsBadRequest` |

### Diagram issue rules (DV)

DV-01 to DV-17 never produce an HTTP error by themselves. Their messages and owners are listed in
[services.md](services.md#diagramvalidator). One test fixture per code exists in
`ProjectOptimizerTests/Fixtures/Validation` and is checked by `Validate_FixturePerDvCode_ReportsThatCode`.

## Check order on scenario endpoints

Each check runs only when the previous one passed:

1. Formal 400.
2. 404 unknown project (or a project without a diagram).
3. 409 diagram has errors (BR-11).
4. 409 too many scenarios (BR-12; not applied to `resource-limits`).
5. 404 route index ≥ count (BR-30) for `scenarios/{index}`, or 409 more than 1000 scenarios
   (`AnalysisLimits.MaxListedScenarios`) for `scenarios`.

For diagram save the order is:

1. Formal 400.
2. 404 project.
3. For a JSON save, conversion 400 (`ProcessJsonConverter`).
4. Parser 400 (BR-17, BR-08).

A save never answers 409: it overwrites the diagram, and the last save wins.

## Known differences from the blueprint

The code is the source of truth, and this guide documents the code. The blueprint (`.workflow/blueprint.json`)
differs from it in these places:

| Topic | Blueprint says | Code does |
|---|---|---|
| Name length (VAL-10) | `[Required]` + `[StringLength(100)]` on the raw value | `[TrimmedStringLength(100)]`: the length is checked **after trimming**, so a 100-character name with surrounding spaces is accepted and stored trimmed |
| `isJointWorstCase` | true when the keys of the max-cost and max-critical-path selections are equal | true when the duration of a separate joint worst-case evaluation (exclusive blocks take the costliest branch, ties by longer duration) equals `maxCriticalPathDuration` |
| Diagram storage (BR-06, BR-07, BR-19, IR-02) | append-only diagram revisions with a stale-save check, retention of 50, and revision lookup by number | exactly one `Diagram` per project; saving overwrites it, so these rules do not apply |
| BR-05 location | a separate method in `ProjectService` | `ProjectService.CreateProjectAsync` sets `project.Diagram = diagramFactory.Create(…, DefaultDiagram.Xml, …)` |
| DV check method names | `DiagramValidator.CheckDV02` … `CheckDV17` | `CheckStartEventExists`, `CheckEndEventExists`, `CheckReachableFromStart`, `CheckReachesEnd`, `CheckAcyclic`, `CheckActivityEstimates`, `CheckMixedGateways`, `CheckImplicitSplitsAndJoins`, `CheckIgnoredConditions`, `CheckDuplicateNames`, `CheckInclusiveBranchLimit` |
| Project guard | every service uses `IProjectAccessGuard.GetProjectAsync` | `GetProjectAsync` plus a second method `EnsureExistsAsync` (existence only), used by `DiagramService.GetDiagramAsync` |
| Decimal normalisation | `5` and `5.0` always serialise identically | true for computed values; `maxCost` and `maxDurationBound` read from SQLite serialise as stored (for example `25000.0`) |
| Timestamps | UTC `DateTime` | stored as UTC, but read back from SQLite without `DateTimeKind.Utc`, so they serialise without a `Z` suffix |
