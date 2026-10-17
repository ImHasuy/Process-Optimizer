# Repositories and unit of work

All repositories are registered as **scoped** and share the request's `ProjectOptimizerDbContext`. Services never
use repositories directly; they go through `IUnitOfWork`.

## Repository&lt;TEntity&gt;

`Lib/Repository/Repository.cs` · abstract · implements `IRepository<TEntity>` · constructor takes
`ProjectOptimizerDbContext`.

| Member | Behaviour |
|---|---|
| `protected DbContext` | The injected context |
| `protected Entities` | `DbContext.Set<TEntity>()` |
| `FindAsync(Guid id)` | **Tracked** `FirstOrDefaultAsync` on the shadow-accessed `Id` (`EF.Property<Guid>(entity, "Id")`); `null` if missing |
| `GetRequiredAsync(Guid id)` | `FindAsync`, or throws `NotFoundException.For<TEntity>(id)` → `"<Entity> with id '<id>' was not found."` |
| `ExistsAsync(Guid id)` | `AnyAsync` by id |
| `GetRequiredAsync<TDto>(Guid id)` | `AsNoTracking()` + `Where(id)` + `ProjectToType<TDto>()` + `FirstOrDefaultAsync`, or `NotFoundException.For<TEntity>(id)` |
| `Add(TEntity entity)` | Stages an insert |
| `Remove(TEntity entity)` | Stages a delete |

## ProjectRepository

`Lib/Repository/ProjectRepository.cs` · implements `IProjectRepository`.

| Method | Query |
|---|---|
| `ListAsync()` | No-tracking, `OrderByDescending(UpdatedAt).ThenBy(Id)`, `ProjectToType<ProjectListItemDTO>()`; returns every project as `List<ProjectListItemDTO>` (no paging, search or sort options) |
| `IsNameTakenAsync(string normalizedName, Guid? excludedProjectId)` | `AnyAsync(NormalizedName == normalizedName && Id != excludedProjectId)` |

## DiagramRepository

`Lib/Repository/DiagramRepository.cs` · `sealed` · implements `IDiagramRepository` · constructor takes
`ProjectOptimizerDbContext`. It does **not** derive from `Repository<TEntity>`: the `Diagram` key is `ProjectId`, not
`Id`, and a diagram is only inserted with its project (through `Project.Diagram`) and deleted by cascade, so it needs
no `Add`/`Remove`.

| Method | Query |
|---|---|
| `FindAsync(Guid projectId)` | **Tracked** diagram of the project; `null` if missing. Used to overwrite it and by `DiagramResolver` |
| `FindDtoAsync(Guid projectId)` | No-tracking, `ProjectId` filter, `ProjectToType<DiagramDTO>()` (includes the XML); `null` if missing |

## Unit of work

`Lib/Collection/UnitOfWork.cs` · `sealed` · implements `IUnitOfWork` · constructor takes the DbContext,
`IProjectRepository` and `IDiagramRepository`.

| Member | Behaviour |
|---|---|
| `ProjectRepository`, `DiagramRepository` | The scoped repositories |
| `SaveChangesAsync()` | `dbContext.SaveChangesAsync()` |
| `SaveChangesOrConflictAsync(string conflictMessage)` | Saves; a `DbUpdateException` whose inner `SqliteException` has extended error code 2067 (`SQLITE_CONSTRAINT_UNIQUE`) or 1555 (`SQLITE_CONSTRAINT_PRIMARYKEY`) becomes `ConflictException(conflictMessage)` |

### Transaction boundary

Each service method makes at most **one** `SaveChanges` call, so every write use case is a single implicit SQLite
transaction:

- **Project creation** inserts the project and its diagram together.
- **Diagram save** updates the `Diagrams` row in place and `Projects.UpdatedAt` together. There is no concurrency
  check: of two simultaneous saves, the last one wins.
- **Project deletion** deletes one project row; SQLite cascades to its diagram.

### Guard plus index safety net

The uniqueness rule is checked twice:

| Rule | Guard (normal path) | Unique index (concurrent path) | Message |
|---|---|---|---|
| BR-04 project name | `ProjectService.EnsureNameIsAvailableAsync` | `IX_Projects_NormalizedName` | `A project with this name already exists.` |

The guard handles the normal case with a clear message. The index makes two simultaneous requests end in the same
409 instead of a 500.
