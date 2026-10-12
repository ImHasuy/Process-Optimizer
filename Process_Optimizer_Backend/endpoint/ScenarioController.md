# ScenarioController

Scenarios of the project's current diagram: one per combination of exclusive/inclusive gateway choices. Get the count,
then fetch them one by one (`/scenarios/{index}`, `0 … count-1`) or all at once. A diagram with errors → `409`.

Examples below use this diagram: `Start → Split (xor) → Cheap (2d, 100 EUR) | Fast (1d, 300 EUR) → Join → End`.

## Count

```http
GET /api/projects/{projectId}/scenario-count
```

```json
{ "orderingVersion": 1, "count": 2 }
```

## Get one

```http
GET /api/projects/{projectId}/scenarios/{index}
```

```json
{
  "projectId": "c3bd0471-8550-4202-9154-c5a6b7f653c9",
  "orderingVersion": 1,
  "index": 1,
  "count": 2,
  "key": "Split=Fast",
  "choices": [ { "gatewayId": "Split", "gatewayKind": "ExclusiveGateway", "isControllable": true, "chosenTargetIds": ["Fast"] } ],
  "elementIds": ["Start", "Split", "Fast", "Join", "End"],
  "sequenceFlowIds": ["F1", "F3", "F5", "F6"],
  "activities": [
    { "bpmnId": "Fast", "name": "Fast mover", "kind": "Task", "duration": 1, "cost": 300, "parallelizable": true,
      "earliestStart": 0, "earliestFinish": 1, "latestStart": 0, "latestFinish": 1, "slack": 0, "isCritical": true }
  ],
  "criticalPath": ["Start", "Split", "Fast", "Join", "End"],
  "pathCount": 1,
  "totalDuration": 1,
  "sequentialDuration": 1,
  "totalCost": 300,
  "hasExclusiveActivities": false,
  "hasMissingEstimates": false,
  "timeUnit": "Days",
  "currency": "EUR"
}
```

Errors: `404` index ≥ count.

## Get all

```http
GET /api/projects/{projectId}/scenarios
```

Every scenario, in index order, each in the same shape as **Get one**. More than 1000 scenarios → `409`
("request them one by one").

```json
[
  { "index": 0, "count": 2, "key": "Split=Cheap", "criticalPath": ["Start", "Split", "Cheap", "Join", "End"], "totalDuration": 2, "totalCost": 100, "…": "same fields as Get one" },
  { "index": 1, "count": 2, "key": "Split=Fast",  "criticalPath": ["Start", "Split", "Fast", "Join", "End"],  "totalDuration": 1, "totalCost": 300, "…": "same fields as Get one" }
]
```

## Resource limits

```http
GET /api/projects/{projectId}/resource-limits?basis=Planned
```

`basis`: `Planned` (default) | `Pessimistic`. Min/max over all scenarios.

```json
{
  "projectId": "c3bd0471-8550-4202-9154-c5a6b7f653c9",
  "basis": "Planned",
  "timeUnit": "Days",
  "currency": "EUR",
  "scenarioCount": 2,
  "maxCost": 300,
  "maxCostScenario": { "index": 1, "key": "Split=Fast" },
  "minCost": 100,
  "minCostScenario": { "index": 0, "key": "Split=Cheap" },
  "maxCriticalPathDuration": 2,
  "maxCriticalPathScenario": { "index": 0, "key": "Split=Cheap" },
  "minCriticalPathDuration": 1,
  "minCriticalPathScenario": { "index": 1, "key": "Split=Fast" },
  "maxExclusiveDuration": 0,
  "maxDurationBound": 2,
  "maxSequentialDuration": 2,
  "maxSequentialScenario": { "index": 0, "key": "Split=Cheap" },
  "isJointWorstCase": false,
  "hasExclusiveActivities": false,
  "hasMissingEstimates": false,
  "notes": []
}
```
