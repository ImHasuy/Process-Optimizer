# Projects

Controller: `ProjectController` · route prefix `/api/projects`.

A project holds the settings (name, description, time unit, currency) and exactly one BPMN diagram. Project names are **unique across the whole system**, compared after trimming surrounding spaces and
ignoring case (`Office move` and `  OFFICE MOVE ` are the same name).

**Authorization:** none (phase 1 has no authentication). Every caller can use every project.

- [List projects](#list-projects)
- [Create a project](#create-a-project)
- [Get a project](#get-a-project)
- [Update a project](#update-a-project)
- [Delete a project](#delete-a-project)

---

### List projects

`GET /api/projects`

Returns **all** projects as a plain array, the most recently updated first (ties by `id`). Use it to find a
project's `id`. There are no query parameters: no search, sorting or paging. The database starts empty, so the
array is empty until you [create a project](#create-a-project).

**Responses**

| Status | When | Body |
|---|---|---|
| 200 OK | always (the array may be empty) | `ApiResponse<ProjectListItemDTO[]>` |

`ProjectListItemDTO`

| Field | Type | Description |
|---|---|---|
| `id` | GUID | Project id. |
| `name` | string | Project name. |
| `timeUnit` | `ETimeUnit` | Unit of all durations in the diagram. |
| `currency` | string | ISO 4217 code of all costs in the diagram. |
| `isOptimizable` | boolean | `true` when the diagram has no validation errors. |
| `maxCost` | decimal or `null` | Highest total cost over all scenarios of the diagram (planned values); `null` when the diagram is not optimizable. |
| `maxDurationBound` | decimal or `null` | Guaranteed upper limit of the duration of the diagram (planned values); `null` when not optimizable. |
| `createdAt` | date-time (UTC) | Creation time. |
| `updatedAt` | date-time (UTC) | Last settings change or diagram save. |

```json
{
  "data": [
    {
      "id": "c3bd0471-8550-4202-9154-c5a6b7f653c9",
      "name": "Office move",
      "timeUnit": "Days",
      "currency": "EUR",
      "isOptimizable": true,
      "maxCost": 300.0,
      "maxDurationBound": 2.0,
      "createdAt": "2026-10-11T18:21:57.3681522",
      "updatedAt": "2026-10-11T18:21:57.7956603"
    }
  ],
  "statusCode": 200,
  "message": "Success",
  "date": "2026-10-11T18:21:57.8980918Z",
  "errors": null
}
```

**Example**

```bash
curl http://localhost:5212/api/projects
```

---

### Create a project

`POST /api/projects`

Creates a project with the default diagram (a single start event). The default diagram has error `DV-03` ("The process has no end event."), so the new project is not optimizable until you
[save a diagram](diagram.md#save-the-diagram-from-the-frontend-json-export).

**Request body** (`ProjectCreateDTO`)

| Field | Type | Required | Rules | Description |
|---|---|---|---|---|
| `name` | string | yes | not blank; at most 100 characters after trimming; unique (trimmed, case-insensitive) | Stored trimmed. |
| `description` | string | no | at most 1000 characters | Free text. |
| `timeUnit` | `ETimeUnit` | no | `Minutes`, `Hours`, `Days`, `Weeks` | Default `Days`. |
| `currency` | string | no | three upper-case letters (`^[A-Z]{3}$`) | Default `EUR`. |

```json
{ "name": "Office move", "description": "Moving to the new office.", "timeUnit": "Days", "currency": "EUR" }
```

**Responses**

| Status | When | Body |
|---|---|---|
| 201 Created | created | `ApiResponse<ProjectDetailsDTO>`, `Location: /api/projects/{id}` |
| 400 Bad Request | invalid body (missing/blank name, too long, bad time unit or currency, malformed JSON) | error envelope with `errors` |
| 409 Conflict | a project with this name already exists | error envelope, message `A project with this name already exists.` |

```json
{
  "data": {
    "id": "c3bd0471-8550-4202-9154-c5a6b7f653c9",
    "name": "Office move",
    "description": "Moving to the new office.",
    "timeUnit": "Days",
    "currency": "EUR",
    "createdAt": "2026-10-11T18:21:57.3681522",
    "updatedAt": "2026-10-11T18:21:57.3681522",
    "maxCost": null,
    "maxDurationBound": null,
    "diagram": {
      "updatedAt": "2026-10-11T18:21:57.3681522",
      "sizeBytes": 786,
      "flowNodeCount": 1,
      "errorCount": 1,
      "warningCount": 0,
      "isOptimizable": false
    }
  },
  "statusCode": 201,
  "message": "Success",
  "date": "2026-10-11T18:21:57.5810129Z",
  "errors": null
}
```

**Example**

```bash
curl -X POST http://localhost:5212/api/projects \
  -H "Content-Type: application/json" \
  -d '{"name":"Office move","description":"Moving to the new office.","timeUnit":"Days","currency":"EUR"}'
```

---

### Get a project

`GET /api/projects/{id}`

Returns one project with the summary of its diagram.

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | GUID | yes | The project. |

**Responses**

| Status | When | Body |
|---|---|---|
| 200 OK | found | `ApiResponse<ProjectDetailsDTO>` |
| 404 Not Found | no project with this id | error envelope, `Project with id '…' was not found.` |

`ProjectDetailsDTO`

| Field | Type | Description |
|---|---|---|
| `id` | GUID | Project id. |
| `name` | string | Project name. |
| `description` | string or `null` | Description. |
| `timeUnit` | `ETimeUnit` | Unit of the durations. |
| `currency` | string | ISO 4217 code of the costs. |
| `createdAt` | date-time (UTC) | Creation time. |
| `updatedAt` | date-time (UTC) | Last settings change or diagram save. |
| `maxCost` | decimal or `null` | As in the list (planned values of the diagram). |
| `maxDurationBound` | decimal or `null` | As in the list. |
| `diagram` | `DiagramSummaryDTO` | Summary of the diagram: `updatedAt`, `sizeBytes`, `flowNodeCount`, `errorCount`, `warningCount`, `isOptimizable` (same meaning as in [`DiagramDTO`](diagram.md#get-the-diagram), without the XML). |

```json
{
  "data": {
    "id": "c3bd0471-8550-4202-9154-c5a6b7f653c9",
    "name": "Office move",
    "description": "Moving to the new office.",
    "timeUnit": "Days",
    "currency": "EUR",
    "createdAt": "2026-10-11T18:21:57.3681522",
    "updatedAt": "2026-10-11T18:21:57.7956603",
    "maxCost": 300.0,
    "maxDurationBound": 2.0,
    "diagram": {
      "updatedAt": "2026-10-11T18:21:57.7956603",
      "sizeBytes": 3779,
      "flowNodeCount": 6,
      "errorCount": 0,
      "warningCount": 0,
      "isOptimizable": true
    }
  },
  "statusCode": 200,
  "message": "Success",
  "date": "2026-10-11T18:21:57.9417346Z",
  "errors": null
}
```

**Example**

```bash
curl http://localhost:5212/api/projects/{id}
```

---

### Update a project

`PUT /api/projects/{id}`

Replaces **all** settings of a project (full replacement, not a patch). `updatedAt` is set to the current time.

> Changing `timeUnit` or `currency` does **not** convert the numbers stored in the diagram. A task with
> `po:duration="5"` lasts 5 days before and 5 hours after switching from `Days` to `Hours`.

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | GUID | yes | The project. |

**Request body** (`ProjectUpdateDTO`)

| Field | Type | Required | Rules | Description |
|---|---|---|---|---|
| `name` | string | yes | not blank; at most 100 characters after trimming; unique among the other projects | Stored trimmed. Keeping the current name is allowed. |
| `description` | string | no | at most 1000 characters | `null` or omitted clears the description. |
| `timeUnit` | `ETimeUnit` | yes | `Minutes`, `Hours`, `Days`, `Weeks` | New time unit. |
| `currency` | string | yes | `^[A-Z]{3}$` | New currency. |

```json
{ "name": "Office move", "description": "Moving to the new office.", "timeUnit": "Hours", "currency": "USD" }
```

**Responses**

| Status | When | Body |
|---|---|---|
| 204 No Content | updated | none |
| 400 Bad Request | invalid body (for example `timeUnit` or `currency` missing) | error envelope with `errors` |
| 404 Not Found | no project with this id | error envelope |
| 409 Conflict | another project already has this name | error envelope, `A project with this name already exists.` |

**Example**

```bash
curl -X PUT http://localhost:5212/api/projects/{id} \
  -H "Content-Type: application/json" \
  -d '{"name":"Office move","description":"Moving to the new office.","timeUnit":"Hours","currency":"USD"}'
```

---

### Delete a project

`DELETE /api/projects/{id}`

Deletes the project permanently, together with **its diagram**.

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | GUID | yes | The project. |

**Responses**

| Status | When | Body |
|---|---|---|
| 204 No Content | deleted | none |
| 404 Not Found | no project with this id (also on a second delete) | error envelope |

**Example**

```bash
curl -X DELETE http://localhost:5212/api/projects/{id}
```
