# Project Optimizer API — phase 1

An ASP.NET Core 8 Web API that stores projects modelled as **BPMN 2.0 diagrams**. Activities carry a duration
and a cost through `po:` extension attributes, and every save of the diagram becomes a numbered revision. The API
parses and validates each revision with 17 diagram rules (DV-01 … DV-17). For valid diagrams it enumerates every
**scenario** (one per combination of exclusive/inclusive gateway choices) and computes each scenario's critical
path, total duration and total cost. It also reports the cost and duration **resource limits** over all
scenarios, and stores named snapshots of scenarios.

This repository is **phase 1** of the specification (doc 09 §9.1–9.2). It has no users or authentication, no
project ownership, no dependency matrix and no optimizer; those are phase 2. Every endpoint is anonymous, so
**do not expose this build to the internet**.

## Features

- **Projects** (FR-10 … FR-16): create, list with search / sort / paging, read, replace settings, delete with
  cascade, copy (`"<name> (copy)"`, `" (copy) 2"`, …). Names are globally unique, ignoring case and surrounding
  spaces.
- **Diagram revisions** (FR-17, FR-20 … FR-24, FR-32):
  - Save a BPMN XML as a new revision with optimistic concurrency (`baseRevision`).
  - Or save the frontend's JSON export (`states`, `branches`) through `…/diagram/revisions/json`: it is converted to
    BPMN XML with a generated layout, and texts such as `15m` or `25 EUR` are converted to the project's unit and currency.
  - Read the latest revision, list and read revisions, and download a `.bpmn` file.
  - Each revision stores an issue count summary.
  - The latest 50 revisions are kept (older ones only if a saved scenario uses them).
- **Safe XML handling** (BR-08, BR-17, NFR-15): no DTDs, no XXE, at most 2 MB and 1000 flow nodes.
- **Analysis** (FR-30, FR-31): the parsed process graph and a validation report for any revision, including
  diagrams with errors.
- **Scenarios** (FR-42, FR-43, FR-60 … FR-63):
  - Count the scenarios without building them.
  - List scenarios with paging and sorting by index, cost or duration.
  - Fetch one scenario by its canonical index, with its CPM schedule (ES/EF/LS/LF/slack), critical path, path
    count, total and sequential duration, and total cost.
- **Resource limits** (FR-70 … FR-72): min/max cost and min/max critical-path duration with the scenarios that
  reach them, plus max sequential duration, max exclusive duration and the duration bound. Either the planned or
  the pessimistic estimates can be used.
- **Saved scenarios** (FR-65): frozen, named snapshots with an availability check against the latest revision.
- **Uniform JSON envelope** (`ApiResponse`), Swagger UI with XML docs, migrations and seed data applied on startup.

## Tech stack

| Component | Version |
|---|---|
| .NET / ASP.NET Core | 8.0 (`net8.0`) |
| EF Core + SQLite | 8.0.31 |
| Mapster | 10.0.13 |
| Swashbuckle.AspNetCore | 10.3.0 |
| xUnit | 2.5.3 |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.31 |
| Microsoft.Extensions.TimeProvider.Testing | 10.10.0 |

## Prerequisites

- .NET SDK **8.0** (built and tested with 8.0.425).
- Nothing else: SQLite is embedded, and `dotnet-ef` is a local tool.

## Run it

```bash
dotnet tool restore
dotnet run --project ProjectOptimizerAPI
```

The `http` launch profile listens on `http://localhost:5212` and opens **Swagger UI at
`http://localhost:5212/swagger`** (Development environment). The `https` profile adds `https://localhost:7236`:

```bash
dotnet run --project ProjectOptimizerAPI --launch-profile https
```

Quick check:

```bash
curl http://localhost:5212/api/health
curl "http://localhost:5212/api/projects?search=web"
```

`ProjectOptimizerAPI/ProjectOptimizerAPI.http` has a ready-made request for every endpoint.

## Database & migrations

- **File.** SQLite, connection string `Data Source=projectoptimizer.db`. With `dotnet run --project
  ProjectOptimizerAPI` the file is `ProjectOptimizerAPI/projectoptimizer.db`.
- **Startup.** On startup the API applies the EF Core migrations (`InitialCreate`) and, when the database has no
  projects, seeds two of them:
  - **Web shop launch.** Revision 1 is the default diagram. Revision 2 is the specification's worked example: 2
    scenarios, `G3=E` with 21 days / 23 000 EUR and `G3=F` with 20 days / 25 000 EUR.
  - **Sample project.** Only the default diagram, which reports DV-03 "no end event", so its scenario endpoints
    answer 409.
- **Manual commands.**

  ```bash
  dotnet tool restore
  dotnet ef migrations list --project ProjectOptimizerAPI
  dotnet ef migrations add <Name> --project ProjectOptimizerAPI --output-dir Migrations
  dotnet ef database update --project ProjectOptimizerAPI
  ```

- **Reset the local database.** Stop the API, delete `ProjectOptimizerAPI/projectoptimizer.db` (and any `-shm` /
  `-wal` files), and start the API again.

## Test accounts

None. Phase 1 has no authentication.

## Running the tests

```bash
dotnet test ProjectOptimizer.sln
```

There are 570 test methods (921 test cases with theory rows): unit tests for the analysis engine and the
services, and integration tests over HTTP with `WebApplicationFactory`. All tests use in-memory SQLite. The last
local run (2026-10-11) reported 921 passed, 0 failed.

## Project structure

```
ProjectOptimizer.sln
ProjectOptimizerAPI/      web project: Controllers/, Lib/{Analysis,Collection,Database,Exceptions,Helper,
                          Interface,Mapping,Middleware,Repository,Service}/, Migrations/, Program.cs
ProjectOptimizerLib/      class library: DTO/, Enum/, Model/
ProjectOptimizerTests/    xUnit: Fixtures/, Infrastructure/, Unit/{Analysis,Service}/, Integration/Controller/
Docs/EndUser/             guide for API consumers
Docs/Developer/           guide for maintainers
```

The full tree with a file-by-file table is in [Docs/Developer/project-structure.md](Docs/Developer/project-structure.md).

## Design decisions

| Topic | Decision | Reason |
|---|---|---|
| Scope | Phase 1 only; users/auth, ownership, matrix and optimization are not built | Binding user note, spec doc 09 |
| Authentication | None; all endpoints anonymous | Phase-1 deviation table |
| Project names | No owner; names unique globally (trimmed, case-insensitive), guard + unique index | BR-04 as changed for phase 1 |
| Layering | Controller → Service → UnitOfWork → Repository → DbContext; one project guard for all project-scoped calls | Playbook; phase-2 ownership hook (doc 09 §9.4) |
| Diagram storage | Append-only revisions with the verbatim XML, SHA-256, summary and planned limits | Spec 01 §1.5, 04 §4.2 |
| Hard vs. soft validation | Only size, node count, malformed XML / DTD and a non-BPMN root are rejected (400); DV issues are reported, never block saving | Spec 01 §1.6–1.8 (BR-09) |
| Analysis engine | Pure singleton components in `Lib/Analysis`, results cached in `IMemoryCache` by content hash | Spec 04 §4.3, 08 §8.5 |
| Scenario order | Deterministic canonical index (ordering version 1) over a single-entry single-exit structure tree; keys like `G3=F` | Spec 02 §2.5, 08 §8.3 |
| Numbers | `decimal`, invariant culture, normalised output | Byte-identical results (NFR-18) |
| Status codes | Unknown revision in a query/body → 400; in the route → 404; diagram with errors → 409 | BR-19, spec 04 §4.5/§4.7 |
| Deletes | Hard delete with cascade to revisions and saved scenarios | BR-16, BR-34 |
| Enums | Strings in JSON and in the database | Readability, VAL-12/14/60 |
| API docs | Swashbuckle + XML comments, Development only | .NET 8 SDK |

More decisions with their reasons are in [Docs/Developer/architecture.md](Docs/Developer/architecture.md#design-decisions).

## Documentation

- **[Docs/EndUser/README.md](Docs/EndUser/README.md)** — for API consumers: getting started, workflows, every
  endpoint, errors, data reference.
- **[Docs/Developer/README.md](Docs/Developer/README.md)** — for maintainers: architecture, controllers, services,
  analysis engine, repositories, DTOs and mapping, database, validation rules, testing, configuration, extending.

## Specification coverage

| Spec item | Implemented by |
|---|---|
| FR-10 … FR-16 projects | `GET/POST /api/projects`, `GET/PUT/DELETE /api/projects/{id}`, `POST /api/projects/{id}/copies`; BR-04, BR-05, BR-15, BR-16, BR-23 |
| FR-17, FR-20 … FR-24 diagram | `/api/projects/{projectId}/diagram`, `…/diagram/revisions`, `…/diagram/revisions/{revisionNumber}`, `…/diagram/file`; BR-06, BR-07, BR-08, BR-09, BR-17 |
| FR-30, FR-31 analysis | `GET …/graph`, `GET …/validation`; DV-01 … DV-17, BR-19 |
| FR-42, FR-43, FR-60 … FR-63 scenarios | `GET …/scenario-count`, `GET …/scenarios`, `GET …/scenarios/{index}`; BR-11, BR-12, BR-30 |
| FR-65 saved scenarios | `…/saved-scenarios` CRUD; BR-32, BR-33, BR-34 |
| FR-70 … FR-72 resource limits | `GET …/resource-limits`, stored `maxCost` / `maxDurationBound` per revision |
| NFR-04 / 05 / 06 / 07 / 08 / 15 / 17 / 18 | health endpoint, Swagger, migrations + seed, CORS, request-size limit, XXE-safe parser, analysis cache, deterministic ordering |
| Not implemented (phase 2) | FR-01 … FR-05 (users/auth), FR-40 … FR-45 (matrix), FR-50 … FR-55 (optimization), BR-01/02/03/18/20 … 22/24 … 26, the User and OptimizationRun entities |
| Not implemented (optional) | NFR-19 HTTP caching headers |

The full trace from each requirement to its code and tests is in `.workflow/blueprint.json` (`trace`,
`notImplemented`). The extracted requirements are in [.workflow/requirements.md](.workflow/requirements.md). The
workflow's final audit report is `.workflow/REPORT.md`.

## AI usage

The Claude Code `build-dotnet-api` workflow generated this backend, its tests and this documentation from the
specification. The workflow went through a requirements analysis, an architecture blueprint, parallel
feature builders and test writers, and review rounds. Its gates checked the result: a build with
`-warnaserror`, a comment-policy check, the full test suite, a smoke run that fetches the OpenAPI document and the
Swagger UI, and a documentation coverage check.
