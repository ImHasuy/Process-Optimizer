# Process-Optimizer

Model a business process as a BPMN diagram, give each task a time and a price, and let the backend work out every
possible scenario with its critical path, duration and cost.

| Part | What it is | Docs |
|---|---|---|
| `Process_Optimizer_Frontend/` | React + bpmn-js editor that exports the diagram as JSON (`states`, `branches`) | [Frontend README](Process_Optimizer_Frontend/README.md) |
| `Process_Optimizer_Backend/` | ASP.NET Core 8 API that stores the diagrams, validates them and computes the scenarios | [Backend README](Process_Optimizer_Backend/README.md) |

Quick start:

```bash
# backend: http://localhost:5212 (Swagger at /swagger)
cd Process_Optimizer_Backend && dotnet tool restore && dotnet run --project ProjectOptimizerAPI

# frontend: http://localhost:5173
cd Process_Optimizer_Frontend && npm install && npm run dev
```

The frontend's JSON export is saved with `POST /api/projects/{projectId}/diagram/revisions/json`; see the backend
README for the format and all endpoints.
