# Errors

## The error envelope

Every error the API produces has the same JSON shape as a success response, without `data`:

```json
{
  "statusCode": 409,
  "message": "A project with this name already exists.",
  "date": "2026-10-10T19:14:51.9780009Z",
  "errors": null
}
```

When the request fails **field validation** (body, query or route values), `message` is
`One or more validation errors occurred.` and `errors` maps each failing field to its messages. Field names are
those of the C# model (`Name`, `Currency`, `Basis`, …); JSON syntax errors use a JSON path (`$`,
`$.timeUnit`).

```json
{
  "statusCode": 400,
  "message": "One or more validation errors occurred.",
  "date": "2026-10-11T18:22:25.2465666Z",
  "errors": {
    "Name": ["The project name is required."],
    "Currency": ["The currency must be a three-letter upper-case ISO 4217 code."]
  }
}
```

A URL that matches no route at all (unknown path, or a path value of the wrong type such as an `id` that is not a
GUID) is answered by the framework with `404` and an empty body.

## Status codes

| Status | Typical causes | What to do |
|---|---|---|
| 400 Bad Request | Field validation (`errors` filled); malformed JSON; negative scenario index; diagram XML unreadable; invalid ids or types in the frontend JSON | Fix the request as described by `message` / `errors`. |
| 404 Not Found | Unknown project id; scenario index in the route ≥ scenario count | Check the ids: list projects first; use an index below the scenario count. |
| 409 Conflict | Duplicate project name; scenario or resource-limit request on a diagram with errors or with too many scenarios | Choose another name; fix the diagram (see the [validation rules](data-reference.md#diagram-validation-codes)); request scenarios one by one. |
| 413 Payload Too Large | Diagram save request body larger than 3 MiB | Reduce the diagram (the XML itself may be at most 2 MB). |
| 500 Internal Server Error | Unexpected failure; message `An unexpected error occurred.` | Retry; report it if it persists. |

## Business-rule and request errors

Every message below is returned verbatim in `message`.

| Status | Message | When it happens | How to fix the request |
|---|---|---|---|
| 404 | `Project with id '<id>' was not found.` | Any `/api/projects/{id}…` or `/api/projects/{projectId}/…` call with an id that does not exist (or was deleted). | Use an id from `GET /api/projects`. |
| 409 | `A project with this name already exists.` | `POST /api/projects` or `PUT /api/projects/{id}` with a name another project already has (trimmed, case-insensitive). | Choose another name. |
| 400 | `The diagram XML must not be larger than 2 MB.` | A diagram save whose XML (sent or generated from the frontend JSON) has more than 2 097 152 UTF-8 bytes. | Simplify the diagram (remove unused diagram-interchange data, documentation, …). |
| 400 | `The diagram is not well-formed XML or contains a DTD.` | The XML cannot be parsed, or it contains a `<!DOCTYPE …>`. | Send well-formed XML without a DTD. |
| 400 | `The diagram root element must be a BPMN 2.0 definitions element.` | The root element is not `definitions` in namespace `http://www.omg.org/spec/BPMN/20100524/MODEL`. | Export the diagram as BPMN 2.0 XML. |
| 400 | `The id '<id>' is not a valid BPMN identifier.` | `PUT …/diagram/json` with a state or branch id that is not an XML NCName (starts with a digit, contains spaces or `:`, …). | Use ids such as `Activity_1`. |
| 400 | `The id '<id>' is used more than once.` | `PUT …/diagram/json` with two states/branches sharing an id. | Make the ids unique. |
| 400 | `The type '<type>' of state '<id>' is not a BPMN element type such as 'bpmn:Task'.` | `PUT …/diagram/json` with a `type` that is not `bpmn:` + a PascalCase name. | Send the bpmn-js element type, e.g. `bpmn:ExclusiveGateway`. |
| 400 | `The state '<id>' has more than one 'nor' (default) branch.` | `PUT …/diagram/json` with two `nor` branches leaving the same state. | Keep one default branch per state. |
| 400 | `The diagram must not contain more than 1000 flow nodes.` | More than 1000 flow nodes (events, tasks, gateways, sub-processes, …) in the processes, also in the XML generated from the frontend JSON. | Split or simplify the process. |
| 409 | `The diagram has errors and cannot be analysed.` | `scenario-count`, `scenarios`, `scenarios/{index}` or `resource-limits` while the diagram's `errorCount` is above 0. | Fix the errors (see the [validation rules](data-reference.md#diagram-validation-codes)) and save the diagram again. |
| 409 | `The diagram has too many scenarios to index.` | `scenario-count`, `scenarios` or `scenarios/{index}` on a diagram with more than 9 007 199 254 740 991 scenarios. | Reduce the number of independent choices; `resource-limits` still works. |
| 409 | `The diagram has more than 1000 scenarios; request them one by one.` | `GET …/scenarios` (the list) on a diagram with more than 1000 scenarios. | Call `GET …/scenario-count`, then `GET …/scenarios/{index}` for each index from 0 to count − 1. |
| 404 | `The scenario does not exist for this diagram.` | `GET …/scenarios/{index}` with `index` ≥ scenario count. | Use an index below `GET …/scenario-count`. |
| 404 | `The project has no diagram.` | Any diagram or scenario call on a project whose diagram row is missing. Should not happen: every project is created with a diagram. | Report it; recreating the project fixes it. |
| 413 | `The request body is too large.` | Diagram save request body over 3 MiB. | Send a smaller diagram. |
| 400 | `The request is malformed.` | The server could not read the request (bad HTTP request). | Check the request framing and headers. |
| 500 | `An unexpected error occurred.` | Unhandled server error. | Retry; report it. |

## Field validation messages

These appear inside `errors` with the message `One or more validation errors occurred.`.

| Field | Message | Endpoint(s) |
|---|---|---|
| `Name` | `The project name is required.` | create / update project (missing, empty or blank) |
| `Name` | `The project name must be at most 100 characters long.` | create / update project (after trimming) |
| `Description` | `The description must be at most 1000 characters long.` | create / update project |
| `TimeUnit` | `The time unit is required.` | update project |
| `TimeUnit` | `The time unit must be Minutes, Hours, Days or Weeks.` | create / update project (undefined numeric value) |
| `Currency` | `The currency is required.` | update project |
| `Currency` | `The currency must be a three-letter upper-case ISO 4217 code.` | create / update project |
| `Basis` | `The basis must be Planned or Pessimistic.` | resource limits (undefined numeric value) |
| `BpmnXml` | `The diagram XML is required.` | save diagram (XML) |
| `States[i].Id` / `Branches[i].Id` | `The state id is required.` / `The branch id is required.` (and `… must be at most 255 characters long.`) | save diagram from JSON |
| `States[i].Type` | `The state type is required.` / `The state type must be at most 100 characters long.` | save diagram from JSON |
| `Branches[i].Source` / `Target` | `The branch source is required.` / `The branch target is required.` | save diagram from JSON |
| `States[i].Name`, `Time`, `Price`, `Branches[i].Name`, `Type`, `Condition` | `The … must be at most <n> characters long.` | save diagram from JSON |
| `index` | `The scenario index must be zero or greater.` | `GET …/scenarios/{index}` with a negative index |

Messages produced by the framework rather than the API:

| Situation | Example message |
|---|---|
| Enum value in the query string that is not a name of the enum | `The value 'Foo' is not valid for Basis.` |
| Body is not valid JSON | key `$`: `'b' is an invalid start of a property name. Expected a '"'. Path: $ \| LineNumber: 0 \| BytePositionInLine: 1.` |
| Enum value in the body that is not a name of the enum (e.g. `"timeUnit": "Years"`) | key `$.timeUnit`: ``The JSON value could not be converted to System.Nullable`1[ProjectOptimizerLib.Enum.ETimeUnit]. Path: $.timeUnit \| …`` |

## Diagram problems are not request errors

Missing end events, cycles, unsupported elements, invalid `po:` values, `time`/`price` texts that cannot be
converted and similar problems **do not** reject a save. They are only counted on the diagram (`errorCount`,
`warningCount`, `isOptimizable`); no endpoint lists them one by one. As long as `errorCount` is above 0, the
scenario and resource-limit endpoints answer 409 `The diagram has errors and cannot be analysed.`
The rules behind the counts are listed in [data-reference.md](data-reference.md#diagram-validation-codes).
