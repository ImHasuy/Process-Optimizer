# Scenarios and resource limits

Controller: `ScenarioController` · route prefix `/api/projects/{projectId}`.

A **scenario** is one way the process can actually run: at every exclusive or event-based split exactly one
branch is taken, at every inclusive split a non-empty set of branches is taken; parallel branches always all run.
Scenarios are not stored. They are computed from the project's current diagram and identified by their
**index** (0, 1, 2, …) in a fixed, documented order (`orderingVersion` 1) and by a readable **key**, for example
`Split=Fast` (see [data-reference.md](../data-reference.md#scenario-keys-and-indexes)). The same diagram and index
always give the same scenario; after the diagram is saved again, an index may point to a different scenario.

All four endpoints require a diagram **without validation errors** (`errorCount` 0, `isOptimizable` `true`);
otherwise they answer **409** `The diagram has errors and cannot be analysed.`
The scenario-indexed endpoints (all except the resource limits) also answer 409 `The diagram has too many scenarios to index.` when the diagram has
more than 9 007 199 254 740 991 (2^53 − 1) scenarios; the resource limits are still available then.

Durations are in the project's `timeUnit`, costs in its `currency` (the current project settings).

**Authorization:** none (phase 1).

- [Count scenarios](#count-scenarios)
- [Get one scenario](#get-one-scenario)
- [List scenarios](#list-scenarios)
- [Get the resource limits](#get-the-resource-limits)

---

### Count scenarios

`GET /api/projects/{projectId}/scenario-count`

Returns how many scenarios the diagram has, without building any of them. Cheap, even for huge counts.

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `projectId` | GUID | yes | The project. |

**Responses**

| Status | When | Body |
|---|---|---|
| 200 OK | counted | `ApiResponse<ScenarioCountDTO>` |
| 404 Not Found | no project with this id | error envelope |
| 409 Conflict | the diagram has validation errors, or more than 2^53 − 1 scenarios | error envelope |

`ScenarioCountDTO`: `orderingVersion` (integer, currently `1`), `count` (integer).

```json
{
  "data": { "orderingVersion": 1, "count": 2 },
  "statusCode": 200,
  "message": "Success",
  "date": "2026-10-11T18:22:06.2599504Z",
  "errors": null
}
```

**Example**

```bash
curl http://localhost:5212/api/projects/{projectId}/scenario-count
```

---

### Get one scenario

`GET /api/projects/{projectId}/scenarios/{index}`

Returns one scenario with its elements, a critical-path schedule of its activities and its totals. Use
`elementIds` / `sequenceFlowIds` to highlight the scenario in the diagram editor, and `activities` for a table
or Gantt chart.

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `projectId` | GUID | yes | The project. |
| `index` | integer (64-bit) | yes | Zero-based canonical index; must be below the scenario count. |

**Responses**

| Status | When | Body |
|---|---|---|
| 200 OK | found | `ApiResponse<ScenarioDTO>` |
| 400 Bad Request | `index` < 0 (`The scenario index must be zero or greater.`) | error envelope |
| 404 Not Found | no project with this id, or `index` ≥ scenario count (`The scenario does not exist for this diagram.`) | error envelope |
| 409 Conflict | the diagram has validation errors, or more than 2^53 − 1 scenarios | error envelope |

`ScenarioDTO`

| Field | Type | Description |
|---|---|---|
| `projectId` | GUID | The project. |
| `orderingVersion` | integer | Version of the ordering rules (`1`). |
| `index` | integer | Canonical index. |
| `count` | integer | Total number of scenarios of the diagram. |
| `key` | string | Scenario key (empty when there are no choices). |
| `choices` | `ScenarioChoiceDTO[]` | One entry per decision, in enumeration order. |
| `elementIds` | string[] | Ids of all flow nodes in the scenario, in topological order. |
| `sequenceFlowIds` | string[] | Ids of all sequence flows in the scenario. |
| `activities` | `ScenarioActivityDTO[]` | Activities (and any other node with a duration or cost), in topological order, with CPM times. |
| `criticalPath` | string[] | Node ids of the longest path from start to end. |
| `pathCount` | integer | Number of distinct start-to-end paths in the scenario. |
| `totalDuration` | decimal | Critical-path length (parallel branches really run in parallel). |
| `sequentialDuration` | decimal | Sum of all durations, as if everything ran one after another. |
| `totalCost` | decimal | Sum of all costs. |
| `hasExclusiveActivities` | boolean | `true` when an activity with `po:parallelizable="false"` is in the scenario. |
| `hasMissingEstimates` | boolean | `true` when an activity in the scenario lacks `po:duration` or `po:cost`. |
| `timeUnit` | `ETimeUnit` | Project time unit. |
| `currency` | string | Project currency. |

`ScenarioChoiceDTO`

| Field | Type | Description |
|---|---|---|
| `gatewayId` | string | Id of the splitting gateway (or of the node that starts the choice). |
| `gatewayKind` | `EFlowNodeKind` | `ExclusiveGateway`, `InclusiveGateway` or `EventBasedGateway`. |
| `isControllable` | boolean | `false` for event-based gateways: the branch is decided by an external event, not by the planner. |
| `chosenTargetIds` | string[] | Id of the first node of each chosen branch (one for exclusive, one or more for inclusive), sorted. |

`ScenarioActivityDTO`

| Field | Type | Description |
|---|---|---|
| `bpmnId`, `name`, `kind` | string, string, `EFlowNodeKind` | The node. |
| `duration`, `cost` | decimal | Values from the diagram. |
| `parallelizable` | boolean | `false` for exclusive activities. |
| `earliestStart`, `earliestFinish` | decimal | Earliest start/finish (forward pass). |
| `latestStart`, `latestFinish` | decimal | Latest start/finish without delaying the end (backward pass). |
| `slack` | decimal | `latestStart − earliestStart`. |
| `isCritical` | boolean | `true` when the activity has no slack. |

```json
{
  "data": {
    "projectId": "c3bd0471-8550-4202-9154-c5a6b7f653c9",
    "orderingVersion": 1,
    "index": 1,
    "count": 2,
    "key": "Split=Fast",
    "choices": [ { "gatewayId": "Split", "gatewayKind": "ExclusiveGateway", "isControllable": true, "chosenTargetIds": ["Fast"] } ],
    "elementIds": ["Start", "Split", "Fast", "Join", "End"],
    "sequenceFlowIds": ["F1", "F3", "F5", "F6"],
    "activities": [
      { "bpmnId": "Fast", "name": "Fast mover", "kind": "Task", "duration": 1, "cost": 300, "parallelizable": true, "earliestStart": 0, "earliestFinish": 1, "latestStart": 0, "latestFinish": 1, "slack": 0, "isCritical": true }
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
  },
  "statusCode": 200,
  "message": "Success",
  "date": "2026-10-11T18:22:06.382034Z",
  "errors": null
}
```

**Example**

```bash
curl "http://localhost:5212/api/projects/{projectId}/scenarios/1"
```

---

### List scenarios

`GET /api/projects/{projectId}/scenarios`

Returns **all** scenarios of the diagram as an array of full [`ScenarioDTO`](#get-one-scenario) objects (the same
shape as [Get one scenario](#get-one-scenario)), in canonical index order (ascending). There is no paging and no
sorting parameter; to find the cheapest or fastest scenarios use [the resource limits](#get-the-resource-limits) or
sort on the client.

At most **1000** scenarios are listed. Call [Count scenarios](#count-scenarios) first: if `count` is above 1000,
request the scenarios one by one with [Get one scenario](#get-one-scenario) (indexes `0` … `count − 1`).

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `projectId` | GUID | yes | The project. |

**Responses**

| Status | When | Body |
|---|---|---|
| 200 OK | listed | `ApiResponse<ScenarioDTO[]>` |
| 404 Not Found | no project with this id | error envelope |
| 409 Conflict | the diagram has validation errors, more than 2^53 − 1 scenarios, or more than 1000 scenarios (`The diagram has more than 1000 scenarios; request them one by one.`) | error envelope |

```json
{
  "data": [
    {
      "projectId": "c3bd0471-8550-4202-9154-c5a6b7f653c9",
      "orderingVersion": 1,
      "index": 0,
      "count": 2,
      "key": "Split=Cheap",
      "choices": [ { "gatewayId": "Split", "gatewayKind": "ExclusiveGateway", "isControllable": true, "chosenTargetIds": ["Cheap"] } ],
      "elementIds": ["Start", "Split", "Cheap", "Join", "End"],
      "sequenceFlowIds": ["F1", "F2", "F4", "F6"],
      "activities": [
        { "bpmnId": "Cheap", "name": "Cheap mover", "kind": "Task", "duration": 2, "cost": 100, "parallelizable": true, "earliestStart": 0, "earliestFinish": 2, "latestStart": 0, "latestFinish": 2, "slack": 0, "isCritical": true }
      ],
      "criticalPath": ["Start", "Split", "Cheap", "Join", "End"],
      "pathCount": 1,
      "totalDuration": 2,
      "sequentialDuration": 2,
      "totalCost": 100,
      "hasExclusiveActivities": false,
      "hasMissingEstimates": false,
      "timeUnit": "Days",
      "currency": "EUR"
    },
    {
      "projectId": "c3bd0471-8550-4202-9154-c5a6b7f653c9",
      "orderingVersion": 1,
      "index": 1,
      "count": 2,
      "key": "Split=Fast",
      "…": "same fields as in the Get one scenario example above"
    }
  ],
  "statusCode": 200,
  "message": "Success",
  "date": "2026-10-11T18:22:06.3285964Z",
  "errors": null
}
```

**Example**

```bash
curl http://localhost:5212/api/projects/{projectId}/scenarios
```

---

### Get the resource limits

`GET /api/projects/{projectId}/resource-limits`

Answers "at most how long, at most how expensive" over **all** scenarios of the diagram, without enumerating them,
and names the scenario that reaches each limit. Works for any number of scenarios: above 2^53 − 1 scenarios
`scenarioCount` and all `…Scenario` references are `null` and a note says so.

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `projectId` | GUID | yes | The project. |

**Query parameters**

| Name | Type | Required | Rules | Description |
|---|---|---|---|---|
| `basis` | `EResourceLimitBasis` | no | `Planned`, `Pessimistic` | Default `Planned`. `Pessimistic` uses the `pessimistic` value of `po:estimate` (BPMN XML only; the frontend JSON has no estimates) as the duration of activities that have one; costs are the same for both. |

**Responses**

| Status | When | Body |
|---|---|---|
| 200 OK | computed | `ApiResponse<ResourceLimitsDTO>` |
| 400 Bad Request | invalid `basis` | error envelope |
| 404 Not Found | no project with this id | error envelope |
| 409 Conflict | the diagram has validation errors | error envelope |

`ResourceLimitsDTO`

| Field | Type | Description |
|---|---|---|
| `projectId` | GUID | What was analysed. |
| `basis` | `EResourceLimitBasis` | The basis used. |
| `timeUnit`, `currency` | `ETimeUnit`, string | Project settings. |
| `scenarioCount` | integer or `null` | Number of scenarios; `null` above 2^53 − 1. |
| `maxCost` / `maxCostScenario` | decimal / `ScenarioRefDTO` | Highest total cost and the scenario that reaches it. |
| `minCost` / `minCostScenario` | decimal / `ScenarioRefDTO` | Lowest total cost. |
| `maxCriticalPathDuration` / `maxCriticalPathScenario` | decimal / `ScenarioRefDTO` | Longest critical-path duration. |
| `minCriticalPathDuration` / `minCriticalPathScenario` | decimal / `ScenarioRefDTO` | Shortest critical-path duration. |
| `maxExclusiveDuration` | decimal | Largest sum of durations of exclusive (`parallelizable = false`) activities. |
| `maxDurationBound` | decimal | `maxCriticalPathDuration + maxExclusiveDuration`: a guaranteed upper limit on the duration even when exclusive activities cannot overlap with anything. |
| `maxSequentialDuration` / `maxSequentialScenario` | decimal / `ScenarioRefDTO` | Largest sum of all durations (everything done one after another). |
| `isJointWorstCase` | boolean | `true` when one scenario reaches both `maxCost` and `maxCriticalPathDuration`. |
| `hasExclusiveActivities` | boolean | The diagram contains at least one exclusive activity. |
| `hasMissingEstimates` | boolean | At least one activity lacks `po:duration` or `po:cost`. |
| `notes` | string[] | `Some activities have no duration or cost; the limits may be too low.` and/or `The diagram has too many scenarios to index; scenario references are omitted.` |

`ScenarioRefDTO`: `index` (integer) and `key` (string) of a scenario; pass the index to
[Get one scenario](#get-one-scenario).

```json
{
  "data": {
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
  },
  "statusCode": 200,
  "message": "Success",
  "date": "2026-10-11T18:22:06.4300323Z",
  "errors": null
}
```

**Example**

```bash
curl "http://localhost:5212/api/projects/{projectId}/resource-limits?basis=Pessimistic"
```
