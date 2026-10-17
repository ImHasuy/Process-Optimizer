# Diagram

Controller: `DiagramController` · route prefix `/api/projects/{projectId}/diagram`.

Each project has exactly **one** BPMN diagram. Every save **overwrites** it; there is no revision history and no
concurrency check (the last save wins).

There are two ways to save: the frontend's JSON export
([`PUT …/diagram/json`](#save-the-diagram-from-the-frontend-json-export)), which the API converts to BPMN XML, or a
BPMN XML directly ([`PUT …/diagram`](#save-the-diagram-as-bpmn-xml)). Either way the diagram is stored as BPMN
XML.

Saving never fails because of *semantic* problems in the diagram (missing end event, cycle, …): every save is
validated internally and the diagram stores only the resulting `errorCount`, `warningCount` and `isOptimizable`
(the rules are listed in [data-reference.md](../data-reference.md#diagram-validation-codes); no endpoint lists the
individual issues). A save is rejected only when the body cannot be turned into a readable diagram at all.

**Authorization:** none (phase 1).

- [Get the diagram](#get-the-diagram)
- [Save the diagram from the frontend JSON export](#save-the-diagram-from-the-frontend-json-export)
- [Save the diagram as BPMN XML](#save-the-diagram-as-bpmn-xml)
- [Download the diagram file](#download-the-diagram-file)

---

### Get the diagram

`GET /api/projects/{projectId}/diagram`

Returns the project's diagram including the BPMN XML. Load it into your editor (for example bpmn-js).

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `projectId` | GUID | yes | The project. |

**Responses**

| Status | When | Body |
|---|---|---|
| 200 OK | found | `ApiResponse<DiagramDTO>` |
| 404 Not Found | no project with this id | error envelope |

`DiagramDTO`

| Field | Type | Description |
|---|---|---|
| `updatedAt` | date-time (UTC) | When the diagram was last saved (for a new project: its creation time). |
| `bpmnXml` | string | The BPMN 2.0 XML exactly as it was saved. |
| `contentSha256` | string | Lower-case hex SHA-256 of the UTF-8 XML. |
| `sizeBytes` | integer | Size of the XML in UTF-8 bytes. |
| `flowNodeCount` | integer | Number of BPMN flow nodes in the diagram. |
| `errorCount` | integer | Number of validation errors (rules with severity Error). |
| `warningCount` | integer | Number of validation warnings (rules with severity Warning). |
| `isOptimizable` | boolean | `true` when `errorCount` is 0; only then do the scenario, resource-limit and save-scenario endpoints work (409 otherwise). |

```json
{
  "data": {
    "updatedAt": "2026-10-11T18:21:57.7956603",
    "bpmnXml": "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\" …>…</bpmn:definitions>",
    "contentSha256": "810879da6ec8b277739dd85efdb27a6a10ebfabb9269fdbf8f9dbfccce941d57",
    "sizeBytes": 3779,
    "flowNodeCount": 6,
    "errorCount": 0,
    "warningCount": 0,
    "isOptimizable": true
  },
  "statusCode": 200,
  "message": "Success",
  "date": "2026-10-11T18:22:06.6617991Z",
  "errors": null
}
```

**Example**

```bash
curl http://localhost:5212/api/projects/{projectId}/diagram
```

---

### Save the diagram from the frontend JSON export

`PUT /api/projects/{projectId}/diagram/json`

This is the save the frontend normally uses. It accepts the `{ states, branches }` payload produced by the
frontend's **Save & Export JSON** button, converts it to BPMN 2.0 XML and overwrites the diagram exactly like
[Save the diagram as BPMN XML](#save-the-diagram-as-bpmn-xml), with the same XML limits. The response contains
the **generated** XML, which you can load into bpmn-js.

**Conversion rules**

| JSON | BPMN XML |
|---|---|
| `states[].type` `bpmn:<Type>` | element `bpmn:<type>` (first letter lower-cased), e.g. `bpmn:ExclusiveGateway` → `<bpmn:exclusiveGateway>`. Unsupported types such as `bpmn:ComplexGateway` are kept and reported as `DV-04`. |
| `states[].type` of an artifact (`TextAnnotation`, `Group`, `DataObject`, `DataObjectReference`, `DataStoreReference`, `Participant`, `Lane`, `LaneSet`, `Association`, `DataInputAssociation`, `DataOutputAssociation`, `MessageFlow`, `SequenceFlow`, `Process`, `Collaboration`) | left out (no process semantics) |
| `states[].id`, `name` | `id`, `name` |
| `states[].time` | `po:duration` in the project's `timeUnit` (see below); the original text is kept in `optimizer:time` |
| `states[].price` | `po:cost` in the project's `currency` (see below); the original text is kept in `optimizer:price` |
| `states[].Depends_on`, `Connects_to` | accepted for compatibility with the export, but not used and not stored; the connections come **only** from `branches` |
| `branches[]` | `bpmn:sequenceFlow` with `id`, `sourceRef`, `targetRef`, `name` |
| `branches[].condition` (non-empty) | `<bpmn:conditionExpression xsi:type="bpmn:tFormalExpression">` |
| `branches[].type` = `nor` | the flow becomes the `default` flow of its source state |
| other `branches[].type` values (`sequence`, `xor`, `and`, `or`, …) | ignored: branching semantics come from the **gateway types** (see the note below) |
| — | a diagram layout (`bpmndi`) is generated: nodes in columns by dependency depth, parallel branches in rows |

**Time and price texts**

| Input | Result |
|---|---|
| empty or missing | no attribute (activities then get the `DV-10` warning) |
| plain number (`3`, `2.5`) | used as is, in the project unit / currency |
| number + time unit, also combined (`15m`, `90 min`, `2h`, `1h 30m`, `3 days`, `2w`) | converted to the project's `timeUnit` with 1 d = 24 h and 1 w = 7 d, rounded to 6 decimals; e.g. `15m` in a `Days` project → `0.010417` |
| number + the project currency as code or symbol, before or after (`25 EUR`, `EUR 25`, `€25`, `$50` in a `USD` project) | the number; symbols `$` = USD, `€` = EUR, `£` = GBP |
| anything else (other currency, `1,000`, negative, unknown unit) | kept as typed in `po:duration` / `po:cost`, so the diagram is saved and the value is counted as a **`DV-09`** error (plus the `DV-10` warning, because the activity then has no usable value); amounts are never converted between currencies |

> **Branching note.** The frontend treats a task with several outgoing arrows as an XOR decision. The analysis
> engine follows BPMN and treats it as an implicit **parallel** split (warning `DV-13`). Model decisions with an
> exclusive (or inclusive) gateway and a matching join gateway to get the scenarios you expect.

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `projectId` | GUID | yes | The project. |

**Request body** (`ProcessDiagram`, at most 3 MiB in total)

| Field | Type | Required | Rules | Description |
|---|---|---|---|---|
| `states` | array of `ProcessState` | no | may be empty; missing = empty | The events, activities and gateways. |
| `states[].id` | string | yes | 1–255 characters, a valid XML NCName (letter or `_` first, no spaces or `:`), unique across states and branches | BPMN id. |
| `states[].name` | string | no | ≤ 1000 characters | Label. |
| `states[].type` | string | yes | ≤ 100 characters, `bpmn:` followed by a PascalCase type name | BPMN element type. |
| `states[].time` | string | no | ≤ 100 characters | Duration text, see above. |
| `states[].price` | string | no | ≤ 100 characters | Cost text, see above. |
| `states[].Depends_on` | string[] | no | spelled as exported | Ids of the states with a branch into this state. Not used. |
| `states[].Connects_to` | string[] | no | spelled as exported | Ids of the states this state has a branch to. Not used. |
| `branches` | array of `ProcessBranch` | no | may be empty; missing = empty | The sequence flows; they alone define the connections. |
| `branches[].id` | string | yes | as `states[].id` | BPMN id. |
| `branches[].name` | string | no | ≤ 1000 characters | Label. |
| `branches[].source`, `target` | string | yes | ≤ 255 characters | Ids of the connected states. An unknown id is saved and reported as `DV-05`. |
| `branches[].type` | string | no | ≤ 100 characters; at most one `nor` per source | `sequence`, `xor`, `nor`, `and`, `or` or `complex` as exported; only `nor` (default flow) has an effect, see the conversion rules. |
| `branches[].condition` | string | no | ≤ 1000 characters | Condition expression. |

Property names are matched case-insensitively; unknown properties are ignored.

```json
{
  "states": [
    { "id": "Start", "name": "Start", "type": "bpmn:StartEvent", "time": "", "price": "", "Depends_on": [], "Connects_to": ["Task_1"] },
    { "id": "Task_1", "name": "Pack", "type": "bpmn:Task", "time": "15m", "price": "25 EUR", "Depends_on": ["Start"], "Connects_to": ["End"] },
    { "id": "End", "name": "End", "type": "bpmn:EndEvent", "time": "", "price": "", "Depends_on": ["Task_1"], "Connects_to": [] }
  ],
  "branches": [
    { "id": "Flow_1", "name": "", "source": "Start", "target": "Task_1", "type": "sequence", "condition": null },
    { "id": "Flow_2", "name": "", "source": "Task_1", "target": "End", "type": "sequence", "condition": null }
  ]
}
```

Generated XML for this body in an `Hours` / `EUR` project (excerpt; in a `Days` project `15m` becomes
`0.010417`):

```xml
<bpmn:task id="Task_1" name="Pack" po:duration="0.25" po:cost="25" optimizer:time="15m" optimizer:price="25 EUR" />
```

**Responses**

| Status | When | Body |
|---|---|---|
| 200 OK | saved (overwritten) | `ApiResponse<DiagramDTO>` with the generated `bpmnXml` |
| 400 Bad Request | body invalid (see the rules above); an id is not a valid BPMN identifier or is used twice; a state type is not `bpmn:<Type>`; a state has more than one `nor` branch; the generated XML is larger than 2 MB or has more than 1000 flow nodes | error envelope (with `errors` for body validation) |
| 404 Not Found | no project with this id | error envelope |
| 413 Payload Too Large | the whole request body is larger than 3 MiB | error envelope, `The request body is too large.` |

**Example**

```bash
curl -X PUT http://localhost:5212/api/projects/{projectId}/diagram/json \
  -H "Content-Type: application/json" \
  -d '{"states":[{"id":"S","type":"bpmn:StartEvent"},{"id":"A","type":"bpmn:Task","time":"2h","price":"100"},{"id":"E","type":"bpmn:EndEvent"}],"branches":[{"id":"F1","source":"S","target":"A"},{"id":"F2","source":"A","target":"E"}]}'
```

---

### Save the diagram as BPMN XML

`PUT /api/projects/{projectId}/diagram`

Overwrites the diagram with the XML and updates the project's `updatedAt`. There is no concurrency check: if two
browser tabs or users save, the last save wins.

The XML is checked in this order; the first failing check rejects the request with **400**:

1. at most 2 MB (2 097 152 bytes, UTF-8);
2. well-formed XML without a DTD (`<!DOCTYPE …>` is always rejected);
3. the root element is `definitions` in the BPMN 2.0 model namespace
   `http://www.omg.org/spec/BPMN/20100524/MODEL`;
4. at most 1000 flow nodes.

All other diagram problems are saved and only counted in `errorCount` / `warningCount`.

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `projectId` | GUID | yes | The project. |

**Request body** (`DiagramSaveDTO`, at most 3 MiB in total)

| Field | Type | Required | Rules | Description |
|---|---|---|---|---|
| `bpmnXml` | string | yes | not empty; the checks above | The complete BPMN 2.0 XML. Durations and costs go into `po:` attributes, see [data-reference.md](../data-reference.md#the-po-bpmn-extension). |

```json
{
  "bpmnXml": "<?xml version=\"1.0\" encoding=\"UTF-8\"?><bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\" xmlns:po=\"http://projectoptimizer.app/schema/bpmn/1.0\" id=\"Definitions_1\" targetNamespace=\"http://bpmn.io/schema/bpmn\"><bpmn:process id=\"Process_1\" isExecutable=\"false\"><bpmn:startEvent id=\"Start\" /><bpmn:task id=\"Pack\" name=\"Pack boxes\" po:duration=\"3\" po:cost=\"500\" /><bpmn:task id=\"Move\" name=\"Move furniture\" /><bpmn:endEvent id=\"End\" /><bpmn:sequenceFlow id=\"F1\" sourceRef=\"Start\" targetRef=\"Pack\" /><bpmn:sequenceFlow id=\"F2\" sourceRef=\"Pack\" targetRef=\"Move\" /><bpmn:sequenceFlow id=\"F3\" sourceRef=\"Move\" targetRef=\"End\" /></bpmn:process></bpmn:definitions>"
}
```

**Responses**

| Status | When | Body |
|---|---|---|
| 200 OK | saved (overwritten) | `ApiResponse<DiagramDTO>` |
| 400 Bad Request | body invalid (`bpmnXml` missing or empty); XML larger than 2 MB, not well-formed, contains a DTD, wrong root element, or more than 1000 flow nodes | error envelope (with `errors` for body validation) |
| 404 Not Found | no project with this id | error envelope |
| 413 Payload Too Large | the whole request body is larger than 3 MiB | error envelope, `The request body is too large.` |

```json
{
  "data": {
    "updatedAt": "2026-10-10T19:15:06.51432",
    "bpmnXml": "<?xml version=\"1.0\" encoding=\"UTF-8\"?><bpmn:definitions …>…</bpmn:definitions>",
    "contentSha256": "9d8806b35d6780b97c22a46cd0e282489c0c93c944efe12d09934dc8539ef791",
    "sizeBytes": 677,
    "flowNodeCount": 4,
    "errorCount": 0,
    "warningCount": 1,
    "isOptimizable": true
  },
  "statusCode": 200,
  "message": "Success",
  "date": "2026-10-10T19:15:06.5245438Z",
  "errors": null
}
```

The warning in this example is `DV-10` for task `Move`, which has no `po:duration` / `po:cost`.

**Example**

```bash
curl -X PUT http://localhost:5212/api/projects/{projectId}/diagram \
  -H "Content-Type: application/json" \
  -d @save-diagram.json
```

(`save-diagram.json` contains the request body shown above; the XML must be a JSON string, so its double quotes
are escaped.)

---

### Download the diagram file

`GET /api/projects/{projectId}/diagram/file`

Downloads the diagram as a `.bpmn` file. The body is the raw XML (`Content-Type: application/xml`),
**not** wrapped in the response envelope. The file name is the trimmed project name, with characters that are not
allowed in file names (and `"` and `;`) replaced by `_`, plus `.bpmn`; `diagram.bpmn` if nothing is left.

**Path parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `projectId` | GUID | yes | The project. |

**Responses**

| Status | When | Body |
|---|---|---|
| 200 OK | found | BPMN XML, header `Content-Disposition: attachment; filename="Office move.bpmn"; filename*=UTF-8''Office%20move.bpmn` |
| 404 Not Found | no project with this id | error envelope (JSON) |

**Example**

```bash
curl -OJ http://localhost:5212/api/projects/{projectId}/diagram/file
```
