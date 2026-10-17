# DiagramController

The project's diagram. One per project, stored as BPMN XML. Saving **overwrites** it: no history, last save wins.

## Save frontend JSON

```http
PUT /api/projects/{projectId}/diagram/json
Content-Type: application/json

{
  "states": [
    { "id": "Start",  "name": "Start", "type": "bpmn:StartEvent", "time": "",    "price": "",       "Depends_on": [],         "Connects_to": ["Task_1"] },
    { "id": "Task_1", "name": "Pack",  "type": "bpmn:Task",       "time": "15m", "price": "25 EUR", "Depends_on": ["Start"],  "Connects_to": ["End"] },
    { "id": "End",    "name": "End",   "type": "bpmn:EndEvent",   "time": "",    "price": "",       "Depends_on": ["Task_1"], "Connects_to": [] }
  ],
  "branches": [
    { "id": "Flow_1", "name": "", "source": "Start",  "target": "Task_1", "type": "sequence", "condition": null },
    { "id": "Flow_2", "name": "", "source": "Task_1", "target": "End",    "type": "sequence", "condition": null }
  ]
}
```

| Input | Becomes |
|---|---|
| `"time": "15m"` in a `Days` project | duration `0.010417` |
| `"time": "3"` | duration `3` (already in the project unit) |
| `"price": "25 EUR"` in a `EUR` project | cost `25` |
| `"price": "25 USD"` in a `EUR` project | kept as text, counted as an error |
| branch `"type": "nor"` | default flow of its gateway (max one per state) |
| `Depends_on`, `Connects_to` | ignored; edges come from `branches` |

→ `200`

```json
{
  "updatedAt": "2026-10-11T18:21:57.7956603",
  "bpmnXml": "<?xml version=\"1.0\" encoding=\"UTF-8\"?>…</bpmn:definitions>",
  "contentSha256": "810879da6ec8b277739dd85efdb27a6a10ebfabb9269fdbf8f9dbfccce941d57",
  "sizeBytes": 3779,
  "flowNodeCount": 6,
  "errorCount": 0,
  "warningCount": 0,
  "isOptimizable": true
}
```

Errors: `400` invalid id/type, duplicate id, two `nor` branches from one state · `404` · `413` body > 3 MB.

## Save BPMN XML

```http
PUT /api/projects/{projectId}/diagram
Content-Type: application/json

{
  "bpmnXml": "<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\" xmlns:po=\"http://projectoptimizer.app/schema/bpmn/1.0\"><bpmn:process id=\"P\"><bpmn:startEvent id=\"S\"/><bpmn:task id=\"T\" name=\"Pack\" po:duration=\"3\" po:cost=\"500\"/><bpmn:endEvent id=\"E\"/><bpmn:sequenceFlow id=\"F1\" sourceRef=\"S\" targetRef=\"T\"/><bpmn:sequenceFlow id=\"F2\" sourceRef=\"T\" targetRef=\"E\"/></bpmn:process></bpmn:definitions>"
}
```

Durations and costs go in `po:duration` / `po:cost`. → `200`, same shape as above.

Errors: `400` malformed XML, DTD, not BPMN, > 2 MB or > 1000 nodes · `404`. On `400` the stored diagram is unchanged.

## Get diagram

```http
GET /api/projects/{projectId}/diagram
```

→ same shape as the save response. Errors: `404`.

## Download .bpmn

```http
GET /api/projects/{projectId}/diagram/file
```

→ raw XML (`application/xml`), no envelope, downloaded as `<project name>.bpmn`. Errors: `404`.
