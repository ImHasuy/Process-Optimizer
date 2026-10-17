# Extending the API

This recipe adds a new project-scoped feature end-to-end in the existing style. The running example is a
hypothetical **Milestone** (a named date inside a project); it does not exist in the code. Replace the names for
your own feature.

Rules that apply at every step:

- No comments in C#. The exception is `///` XML docs in `Controllers/` (required on every action) and `DTO/`
  (optional).
- The build must produce zero warnings with `-warnaserror`, with nullable reference types enabled.
- Keep methods small and single-purpose. Extract repeated checks into `Ensure…Async` guards.
- Everything on I/O paths is async.

## 1. Entity

Add a POCO in `ProjectOptimizerLib/Model/Milestone.cs`. It must have no attributes, a `Guid Id`, a `Guid ProjectId`
plus a `Project Project` navigation, and UTC `DateTime` timestamps. Add the inverse collection
`ICollection<Milestone> Milestones` to `Project`.

## 2. Configuration

Add `ProjectOptimizerAPI/Lib/Database/Configuration/MilestoneConfiguration.cs`
(`IEntityTypeConfiguration<Milestone>`). Follow `ProjectConfiguration` and `DiagramConfiguration`:

- `ToTable("Milestones")` and `HasKey`.
- `IsRequired().HasMaxLength(FieldLimits.NameMaxLength)` on strings.
- A unique index such as `(ProjectId, NormalizedName)` for any uniqueness rule.
- `HasOne(Project).WithMany(Milestones).OnDelete(DeleteBehavior.Cascade)`.
- `HasConversion<string>()` for enum columns.

Add a `DbSet<Milestone> Milestones => Set<Milestone>();` to `ProjectOptimizerDbContext`. The configuration is
picked up by `ApplyConfigurationsFromAssembly`.

## 3. Migration

```bash
dotnet tool restore
dotnet ef migrations add AddMilestones --project ProjectOptimizerAPI --output-dir Migrations
```

Review the generated `Up` and `Down`. Never use `EnsureCreated` in the application. The migration is applied
automatically on the next start.

## 4. DTOs

In `ProjectOptimizerLib/DTO`, add these types:

- `MilestoneCreateDTO` and `MilestoneUpdateDTO`: `set` accessors, nullable value types, Data Annotations with
  explicit `ErrorMessage` texts, `[TrimmedStringLength]` for names, and `[EnumDataType]` for enums.
- `MilestoneDTO` and, if needed, `MilestoneListItemDTO`: `init` accessors.

Reuse `FieldLimits`.

## 5. Mapping

Add `Lib/Mapping/MilestoneMappingConfig.cs : IRegister` only if something differs from name-based mapping, and
configure only that difference. When every property maps by name (as for `Diagram` → `DiagramDTO`),
no config class is needed. Keep the expressions translatable to SQL, because repositories use `ProjectToType<T>()`.

## 6. Repository

1. Add `Lib/Interface/IMilestoneRepository.cs : IRepository<Milestone>`, declaring only the feature's own queries,
   such as `ListForProjectAsync` and `IsNameTakenAsync`.
2. Implement it in `Lib/Repository/MilestoneRepository.cs : Repository<Milestone>`, following
   `ProjectRepository`:
   - `AsNoTracking()` for reads.
   - A stable order (`OrderBy…ThenBy(Id)`).
   - `ProjectToType<TDto>()`.
   - Tracked loads only where the service modifies the entity.

## 7. Unit of work

Add `IMilestoneRepository MilestoneRepository { get; }` to `IUnitOfWork`, and inject and expose it in `UnitOfWork`.
For uniqueness rules, call `SaveChangesOrConflictAsync(message)` so that a race on the unique index becomes the
same 409.

## 8. Service

1. Add `Lib/Interface/IMilestoneService.cs` and `Lib/Service/MilestoneService.cs`.
2. Inject `IUnitOfWork`, `IProjectAccessGuard` and `TimeProvider`.
3. Start every method with `projectAccessGuard.GetProjectAsync` or `EnsureExistsAsync` (404 for an unknown
   project; phase 2 adds ownership there).
4. Throw `InvalidRequestException`, `NotFoundException` (`NotFoundException.For<Milestone>(id)`) or
   `ConflictException` from private `Ensure…` guards. Keep messages in `const` fields or in `ErrorMessages` when they
   are shared.
5. Commit once per method.
6. Use `timeProvider.UtcNow()` for timestamps.

If the feature analyses the diagram, depend on `IScenarioCatalog` and `IDiagramAnalyzer` instead of re-parsing.

## 9. Dependency injection

Register the new types in `ServiceCollection.AddLocalServices()`:

```csharp
services.AddScoped<IMilestoneRepository, MilestoneRepository>();
services.AddScoped<IMilestoneService, MilestoneService>();
```

Pure, stateless algorithms belong in `Lib/Analysis`, with their interface in `Lib/Interface`, registered as
singletons in `AddAnalysisComponents()`.

## 10. Controller

Add `Controllers/MilestoneController.cs : ApiControllerBase` with
`[Route("api/projects/{projectId:guid}/milestones")]` and one injected `IMilestoneService`. Each action:

- Uses `[FromBody]` or `[FromQuery]` DTOs and returns `Success(...)`, `Created(nameof(Get…), routeValues, dto)` or
  `NoContent()`.
- Has a `<summary>`, a `<param>` per parameter, `<returns>`, and one `<response code="…">` per status.
- Has a matching `[ProducesResponseType(typeof(ApiResponse<…>) / typeof(ApiResponse), StatusCodes.…)]` per status.

Do not put logic, `try/catch` or `ModelState.IsValid` checks in the controller.

## 11. Tests

- **Service tests** go in `Unit/Service/MilestoneServiceTests.cs : ServiceTestBase`. Add a helper to
  `TestDataBuilder` if you need pre-existing rows. Cover every guard (404 / 400 / 409) and every rule, including "persists
  nothing" on failure.
- **Integration tests** go in `Integration/Controller/MilestoneControllerTests.cs` with
  `IClassFixture<ApiWebApplicationFactory>`. Cover the success status, envelope, `Location`, the formal 400 with
  `AssertValidationErrorAsync(field)`, and the 404 / 409 envelopes with `AssertApiErrorAsync`.
- Run `dotnet test ProjectOptimizer.sln`. Never weaken or delete existing tests.

## Changing the frontend JSON format

The frontend export is modelled by `ProcessDiagram`, `ProcessState` and `ProcessBranch` in
`ProjectOptimizerLib/Model/Process`. To accept a new property:

1. Add it to `ProcessState` or `ProcessBranch` with `[JsonPropertyName("<name as the frontend writes it>")]` and,
   for request validation, Data Annotations with explicit messages (reuse `FieldLimits`).
2. Use it in `ProcessJsonConverter` (`Lib/Analysis`), which turns a `ProcessDiagram` into BPMN XML. If the value has
   to reach the analysis, write it as a BPMN attribute or `po:` extension that `BpmnGraphParser` reads.
3. Add cases to `ProcessDiagramTests` (parse and `ToJson` round trip), `ProcessJsonConverterTests` and, for the HTTP
   path, `DiagramJsonControllerTests`. Extend `ProcessJsonFixtures` when several tests need the same export.

Other code that receives an export as a string reads it with `ProcessDiagram.Parse(json)` and writes it with
`diagram.ToJson()`.

## 12. Documentation

Update both guides:

- **End-user guide**: a new `Docs/EndUser/endpoints/milestones.md` with every action in the endpoint template,
  plus new rows in `errors.md`, `data-reference.md` and `workflows.md`.
- **Developer guide**: `controllers.md`, `services.md`, `repositories-and-unit-of-work.md`, `dtos-and-mapping.md`,
  `database.md` (ER diagram, table, migration), `validation-and-errors.md` (rule rows), `testing.md`,
  `project-structure.md`.
- **Root README**: the features list.

Then run the coverage check from the workflow's playbook scripts folder:

```bash
python3 <playbook>/scripts/check-doc-coverage.py .workflow/openapi.json Docs/EndUser Docs/Developer
```

The `.workflow/openapi.json` document is produced by the workflow's smoke run. When you are working without the
workflow, save `http://localhost:5212/swagger/v1/swagger.json` there instead.
