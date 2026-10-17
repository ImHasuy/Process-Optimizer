# ProjectController

Projects. A new project starts with a default diagram (a lone start event, so not yet optimizable).

## List projects

```http
GET /api/projects
```

Most recently updated first.

```json
[
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
]
```

## Create project

```http
POST /api/projects
Content-Type: application/json

{ "name": "Office move", "description": "Moving to the new office.", "timeUnit": "Days", "currency": "EUR" }
```

| Field | Rule |
|---|---|
| `name` | required, ≤ 100, unique (case-insensitive) |
| `description` | optional, ≤ 1000 |
| `timeUnit` | `Minutes` \| `Hours` \| `Days` \| `Weeks`, default `Days` |
| `currency` | 3 upper-case letters (ISO 4217), default `EUR` |

→ `201`, `Location: /api/projects/{id}`

```json
{
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
}
```

Errors: `400` invalid body · `409` name taken.

## Get project

```http
GET /api/projects/{id}
```

→ same shape as the create response. Errors: `404`.

## Update project

```http
PUT /api/projects/{id}
Content-Type: application/json

{ "name": "Office move", "description": null, "timeUnit": "Hours", "currency": "USD" }
```

All four fields are sent; `name`, `timeUnit` and `currency` are required. Stored diagram numbers are not converted.

→ `204`. Errors: `400` · `404` · `409` name taken.

## Delete project

```http
DELETE /api/projects/{id}
```

Deletes its diagram too. → `204`. Errors: `404`.
