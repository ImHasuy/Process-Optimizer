# Getting started

## 1. Start the API

You need the .NET 8 SDK. From the repository root:

```bash
dotnet tool restore
dotnet run --project ProjectOptimizerAPI --launch-profile http
```

On startup the API applies the database migrations to the SQLite file `projectoptimizer.db` (connection string
`Data Source=projectoptimizer.db` in `appsettings.json`; the path is relative, so the file is created in the folder
you start the API from). The database starts **empty**: there are no demo projects, so the first calls below create
one.

> **Upgrading from an older build?** The database now has a single `InitialCreate` migration (tables `Projects`
> and `Diagrams`). An older `projectoptimizer.db` is not compatible: stop the API, delete the file as
> described in [Resetting the data](#resetting-the-data), and start the API again.

There is no sign-in in phase 1; every endpoint is open.

## 2. Open Swagger

Open **http://localhost:5212/swagger** (the browser opens it automatically with `dotnet run`). Swagger UI is only
enabled in the `Development` environment, which both launch profiles set.

In Swagger, expand an operation, click **Try it out**, fill the parameters and click **Execute**.

## 3. First calls

This is the same flow as `ProjectOptimizerAPI/ProjectOptimizerAPI.http`.

1. **Create a project.** It starts with the default diagram (a single start event, not yet analysable).

   ```bash
   curl -X POST http://localhost:5212/api/projects      -H "Content-Type: application/json"      -d '{"name":"Office move","description":"Moving to the new office.","timeUnit":"Days","currency":"EUR"}'
   ```

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
       "diagram": { "updatedAt": "2026-10-11T18:21:57.3681522", "sizeBytes": 786, "flowNodeCount": 1, "errorCount": 1, "warningCount": 0, "isOptimizable": false }
     },
     "statusCode": 201, "message": "Success", "date": "2026-10-11T18:21:57.5810129Z", "errors": null
   }
   ```

   Ids are generated GUIDs, so yours differ from the ones in this guide. Copy `data.id`; the next steps call it
   `{projectId}`. `GET /api/projects` lists all projects if you lose it.

2. **Save the diagram** in the frontend's JSON export format; this overwrites the default diagram. Put this body
   into `save-diagram.json`:

   ```json
   {
     "states": [
       { "id": "Start", "name": "Start", "type": "bpmn:StartEvent", "time": "", "price": "", "Depends_on": [], "Connects_to": ["Split"] },
       { "id": "Split", "name": "Mover?", "type": "bpmn:ExclusiveGateway", "time": "", "price": "", "Depends_on": ["Start"], "Connects_to": ["Cheap", "Fast"] },
       { "id": "Cheap", "name": "Cheap mover", "type": "bpmn:Task", "time": "2d", "price": "100 EUR", "Depends_on": ["Split"], "Connects_to": ["Join"] },
       { "id": "Fast", "name": "Fast mover", "type": "bpmn:Task", "time": "1d", "price": "300 EUR", "Depends_on": ["Split"], "Connects_to": ["Join"] },
       { "id": "Join", "name": "", "type": "bpmn:ExclusiveGateway", "time": "", "price": "", "Depends_on": ["Cheap", "Fast"], "Connects_to": ["End"] },
       { "id": "End", "name": "Done", "type": "bpmn:EndEvent", "time": "", "price": "", "Depends_on": ["Join"], "Connects_to": [] }
     ],
     "branches": [
       { "id": "F1", "name": "", "source": "Start", "target": "Split", "type": "sequence", "condition": null },
       { "id": "F2", "name": "", "source": "Split", "target": "Cheap", "type": "nor", "condition": null },
       { "id": "F3", "name": "", "source": "Split", "target": "Fast", "type": "xor", "condition": "deadline < 2d" },
       { "id": "F4", "name": "", "source": "Cheap", "target": "Join", "type": "sequence", "condition": null },
       { "id": "F5", "name": "", "source": "Fast", "target": "Join", "type": "sequence", "condition": null },
       { "id": "F6", "name": "", "source": "Join", "target": "End", "type": "sequence", "condition": null }
     ]
   }
   ```

   ```bash
   curl -X PUT http://localhost:5212/api/projects/{projectId}/diagram/json      -H "Content-Type: application/json"      -d @save-diagram.json
   ```

   The answer is 200 with `errorCount` 0, `warningCount` 0, `isOptimizable` `true`, and the generated
   BPMN XML in `bpmnXml`.

3. **List its scenarios.**

   ```bash
   curl "http://localhost:5212/api/projects/{projectId}/scenarios"
   ```

   You get an array of two full scenarios: index `0` with key `Split=Cheap` (2 days, 100) and index `1` with key
   `Split=Fast` (1 day, 300). With more than 1000 scenarios this call answers 409; check
   `GET …/scenario-count` first and request them one by one.

4. **Open one scenario** to see activities, slack and the critical path:

   ```bash
   curl "http://localhost:5212/api/projects/{projectId}/scenarios/1"
   ```

5. **Ask for the resource limits** ("at most how long, at most how expensive"):

   ```bash
   curl "http://localhost:5212/api/projects/{projectId}/resource-limits"
   ```

   `maxCost` is `300`, `maxCriticalPathDuration` is `2`, `maxDurationBound` is `2`.

## 4. Where to go next

- [workflows.md](workflows.md) — recipes such as "edit and save the diagram".
- [endpoints/diagram.md](endpoints/diagram.md) — the frontend JSON format and how `time`/`price` are converted.
- [data-reference.md](data-reference.md) — the `po:` BPMN extension, validation rule codes and limits.
- [errors.md](errors.md) — what each error message means.

The file `ProjectOptimizerAPI/ProjectOptimizerAPI.http` contains ready-made requests for the Visual Studio / Rider
HTTP client.

## Resetting the data

Stop the API, delete `projectoptimizer.db` (and `projectoptimizer.db-shm` / `projectoptimizer.db-wal` if present)
from the folder you started the API from, and start it again; the database is recreated empty.
