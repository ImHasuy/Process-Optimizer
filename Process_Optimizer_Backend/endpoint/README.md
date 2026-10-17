# Endpoints

Base URL: `http://localhost:5212`. JSON in, JSON out.

| Controller | Routes |
|---|---|
| [ProjectController](ProjectController.md) | `/api/projects` |
| [DiagramController](DiagramController.md) | `/api/projects/{projectId}/diagram` |
| [ScenarioController](ScenarioController.md) | `/api/projects/{projectId}/scenario-count`, `/scenarios`, `/resource-limits` |

Every response is wrapped (the examples in these files show only `data`):

```json
{ "statusCode": 200, "message": "Success", "date": "2026-10-11T18:22:06Z", "errors": null, "data": { } }
```

Error:

```json
{ "statusCode": 400, "message": "One or more validation errors occurred.", "errors": { "Name": ["The project name is required."] } }
```

| Status | When |
|---|---|
| 400 | invalid body or query |
| 404 | unknown project or scenario index |
| 409 | duplicate name, diagram has errors, too many scenarios |
