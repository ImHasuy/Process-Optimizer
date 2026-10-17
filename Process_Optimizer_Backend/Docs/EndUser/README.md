# Project Optimizer API — user guide

The Project Optimizer API stores projects whose plan is drawn as a **BPMN 2.0 diagram**. The frontend sends the
diagram as its JSON export (`states` and `branches`, with a `time` and a `price` per task), which the API converts
to BPMN XML; a raw BPMN XML with `po:` duration/cost attributes can be saved as well. Each project has exactly
one diagram and every save overwrites it (there is no revision history). The API checks the diagram internally
against a set of validation rules (`DV-01` … `DV-17`); the diagram only reports how many errors and warnings it has.
When the diagram has no errors, the API enumerates its **scenarios** (one per combination of choices at
exclusive/inclusive gateways), computes the critical path, total duration and cost of each one, and reports
the **resource limits** over all scenarios.

This is **phase 1** of the product: there are no user accounts, no sign-in and no ownership. Every caller can
see and change every project, and project names are unique across the whole system. The database starts empty.

## Base URLs

From `ProjectOptimizerAPI/Properties/launchSettings.json`:

| Launch profile | Base URL |
|---|---|
| `http` (default) | `http://localhost:5212` |
| `https` | `https://localhost:7236` (and `http://localhost:5212`) |

Swagger UI (Development environment only): **`http://localhost:5212/swagger`**. Both profiles open it
automatically. The OpenAPI document is at `http://localhost:5212/swagger/v1/swagger.json`.

## Contents

| File | What you find there |
|---|---|
| [getting-started.md](getting-started.md) | Start the API, open Swagger, make the first calls |
| [workflows.md](workflows.md) | Step-by-step recipes: create a project, save a diagram, explore scenarios, … |
| [endpoints/projects.md](endpoints/projects.md) | List, create, read, update and delete projects |
| [endpoints/diagram.md](endpoints/diagram.md) | Read and save the diagram (frontend JSON or BPMN XML), download the `.bpmn` file |
| [endpoints/scenarios.md](endpoints/scenarios.md) | Scenario count, all scenarios, one scenario, resource limits |
| [errors.md](errors.md) | Error envelope, status codes, every error message and how to fix the request |
| [data-reference.md](data-reference.md) | Enums, field formats, the `po:` BPMN extension, validation codes, limits |

There is no `authentication.md`: phase 1 has no authentication.

## Endpoint overview

| Method and path | Purpose |
|---|---|
| `GET /api/projects` | List all projects, most recently updated first |
| `POST /api/projects` | Create a project (with the default diagram) |
| `GET /api/projects/{id}` | Read one project |
| `PUT /api/projects/{id}` | Replace the project settings |
| `DELETE /api/projects/{id}` | Delete a project with its diagram |
| `GET /api/projects/{projectId}/diagram` | The diagram with its XML |
| `PUT /api/projects/{projectId}/diagram/json` | Overwrite the diagram with the frontend JSON export |
| `PUT /api/projects/{projectId}/diagram` | Overwrite the diagram with a BPMN XML |
| `GET /api/projects/{projectId}/diagram/file` | Download the diagram as a `.bpmn` file |
| `GET /api/projects/{projectId}/scenario-count` | Number of scenarios of the diagram |
| `GET /api/projects/{projectId}/scenarios` | All scenarios (at most 1000), each in full |
| `GET /api/projects/{projectId}/scenarios/{index}` | One scenario with activities and critical path |
| `GET /api/projects/{projectId}/resource-limits` | Min/max cost and duration over all scenarios |

## The response envelope

Every JSON response, success or error, has the same envelope (`ApiResponse`). Field names are camelCase and enum
values are strings.

Success (`ApiResponse<T>`):

```json
{
  "data": { "orderingVersion": 1, "count": 2 },
  "statusCode": 200,
  "message": "Success",
  "date": "2026-10-11T18:22:06.2599504Z",
  "errors": null
}
```

Error (`ApiResponse`, no `data` field):

```json
{
  "statusCode": 404,
  "message": "Project with id '11111111-1111-1111-1111-111111111111' was not found.",
  "date": "2026-10-10T19:14:51.9284993Z",
  "errors": null
}
```

| Field | Type | Meaning |
|---|---|---|
| `data` | object / array | The payload; only on success responses with a body. |
| `statusCode` | integer | Same as the HTTP status code. |
| `message` | string | `"Success"` on success; the error message otherwise. |
| `date` | date-time (UTC) | When the response was created. |
| `errors` | object or `null` | Only for request validation failures: field name → list of messages. `null` otherwise. |

Exceptions to the envelope:

- `204 No Content` responses (`PUT` and `DELETE`) have no body.
- `GET /api/projects/{projectId}/diagram/file` returns the raw BPMN XML (`application/xml`) as a file download.

## Status codes

| Status | Meaning in this API |
|---|---|
| 200 OK | The read or diagram save succeeded; the payload is in `data`. |
| 201 Created | A project was created; the `Location` header points to it. |
| 204 No Content | An update or delete succeeded. |
| 400 Bad Request | The request is invalid: field validation (`errors` filled), bad query value, negative scenario index, unreadable BPMN XML or invalid ids/types in the frontend JSON. |
| 404 Not Found | The project or the scenario index (in the route) does not exist. |
| 409 Conflict | Duplicate project name, or (for scenario and resource-limit requests) the diagram has errors or too many scenarios to index; for the scenario list also more than 1000 scenarios. |
| 413 Payload Too Large | The diagram save body is larger than 3 MiB. |
| 500 Internal Server Error | Unexpected server failure (`"An unexpected error occurred."`). |

Details for every message are in [errors.md](errors.md).
