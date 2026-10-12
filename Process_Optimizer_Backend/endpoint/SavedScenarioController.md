# SavedScenarioController

Named, frozen snapshots of a scenario of the current diagram. The snapshot never changes, even when the diagram is
overwritten later; `isAvailable` / `currentIndex` tell whether (and where) the same choices still exist now.

## Save a scenario

```http
POST /api/projects/{projectId}/saved-scenarios
Content-Type: application/json

{ "scenarioIndex": 1, "name": "Fast mover", "note": "Faster but more expensive." }
```

| Field | Rule |
|---|---|
| `scenarioIndex` | required, < scenario count |
| `name` | required, ≤ 100, unique in the project |
| `note` | optional, ≤ 1000 |

→ `201`

```json
{
  "id": "483ae59d-3e35-4741-9c43-5d96c6ee3cc9",
  "name": "Fast mover",
  "note": "Faster but more expensive.",
  "scenarioIndex": 1,
  "orderingVersion": 1,
  "scenarioKey": "Split=Fast",
  "createdAt": "2026-10-11T18:22:06.7154109Z",
  "isAvailable": true,
  "currentIndex": 1,
  "snapshot": { "key": "Split=Fast", "totalDuration": 1, "totalCost": 300, "…": "full scenario, see ScenarioController › Get one" }
}
```

Errors: `400` index out of range · `404` · `409` name taken or diagram has errors.

## List

```http
GET /api/projects/{projectId}/saved-scenarios
```

Newest first.

```json
[
  {
    "id": "483ae59d-3e35-4741-9c43-5d96c6ee3cc9",
    "name": "Fast mover",
    "scenarioKey": "Split=Fast",
    "totalDuration": 1.0,
    "totalCost": 300.0,
    "isAvailable": true,
    "createdAt": "2026-10-11T18:22:06.7154109"
  }
]
```

## Get one

```http
GET /api/projects/{projectId}/saved-scenarios/{id}
```

→ same shape as the save response. Errors: `404`.

## Rename / change note

```http
PUT /api/projects/{projectId}/saved-scenarios/{id}
Content-Type: application/json

{ "name": "Fast mover (approved)", "note": null }
```

→ `204`. Errors: `400` · `404` · `409` name taken.

## Delete

```http
DELETE /api/projects/{projectId}/saved-scenarios/{id}
```

→ `204`. Errors: `404`.
