# Process Optimizer - Frontend

A modern, object-oriented BPMN 2.0 flowchart and process modeling web application built with **React**, **TypeScript**, **Vite**, and **bpmn-js**.

This frontend enables users to model business and operational workflows, assign custom execution metrics (**Time** and **Price**) to individual tasks, configure branch decision logic (**XOR**, **NOR**, **AND**, **OR**), and export a clean directed acyclic graph (DAG) structure formatted specifically for optimization backends.

---

## 🚀 Quickstart Guide

Follow these steps to initialize and run the project locally.

### 1. Prerequisites

Ensure you have the following installed on your machine:
- **Node.js** (v18 or higher recommended): [Download Node.js](https://nodejs.org/)
- **npm** (comes bundled with Node.js)

### 2. Navigate to the Frontend Directory

Open your terminal and make sure you are in the `Process_Optimizer_Frontend` directory:

```bash
cd Process_Optimizer_Frontend
```

### 3. Install Dependencies

Install all required packages (including `bpmn-js`, `bpmn-js-properties-panel`, and `@bpmn-io/properties-panel`):

```bash
npm install
```

### 4. Start the Development Server

Launch the Vite local development server:

```bash
npm run dev
```

Once running, Vite will display the local URL in your terminal (typically `http://localhost:5173`). Open this URL in your web browser to start using the modeler.

---

## 🛠 Available Scripts

In the project directory, you can run:

| Command | Description |
| :--- | :--- |
| `npm run dev` | Starts the Vite development server with Hot Module Replacement (HMR). |
| `npm run build` | Compiles TypeScript and builds the production-ready application into `dist/`. |
| `npm run preview` | Locally serves the production build for verification. |
| `npm run lint` | Runs the Oxlint static analysis tool across the codebase. |

---

## 📁 Project Architecture

The codebase is organized following Object-Oriented and modular design patterns:

```
src/
├── components/                  # React UI Presentation Layer
│   ├── BpmnCanvas.tsx           # BPMN canvas & properties panel lifecycle container
│   ├── HeaderBar.tsx            # Top application bar with Save & Export action
│   └── JsonPreviewModal.tsx     # Modal displaying the exported JSON graph & copy tool
├── models/                      # Domain Models & Schemas
│   ├── ProcessTypes.ts          # TypeScript interfaces (StateNode, Branch, ProcessExportData)
│   └── customModdle.ts          # BPMN Moddle extension schema (time, price, branchType)
├── properties-panel/            # Custom BPMN Properties Panel Extension
│   ├── entries/                 # Individual field editors
│   │   ├── TimeEntry.ts         # Duration input field (e.g. 15m, 2h)
│   │   ├── PriceEntry.ts        # Cost input field (e.g. $50, 100 EUR)
│   │   └── BranchTypeEntry.ts   # Dropdown for branch semantics (XOR, NOR, AND, OR, sequence)
│   ├── CustomPropertiesProvider.ts # OOP Provider class managing panel entries and groups
│   └── index.ts                 # Module bundle for diagram-js injection
├── services/                    # Business Logic & Process Services
│   └── ProcessExporter.ts       # OOP Service class extracting DAG dependencies & branch types
├── constants/                   # Static Configuration & Templates
│   └── initialDiagramXml.ts     # Default starter BPMN 2.0 diagram
├── App.tsx                      # Main application orchestrator
├── App.css                      # Application styling
├── declarations.d.ts            # Type declarations for third-party libraries
├── index.css                    # Base styling and color themes
└── main.tsx                     # React application entry point
```

---

## 💡 How It Works

### 1. Diagram Modeling
- The main whiteboard allows you to create, drag, connect, and delete BPMN elements (Start Events, Tasks, Gateways, End Events).
- Select any element on the canvas to open its properties in the panel on the right.

### 2. Custom Metrics (Time & Price)
- Click on any activity/task to reveal the **Properties** group in the panel.
- Enter the estimated **Time** (e.g. `30m`, `2h`) and **Price** (e.g. `100 EUR`, `$50`).
- Values are bound to custom Moddle attributes (`optimizer:time`, `optimizer:price`) supporting undo/redo.

### 3. Branching Logic (XOR, NOR, etc.)
- When clicking on any arrow (Sequence Flow) connecting elements, the properties panel allows you to configure its **Branch Type**:
  - `xor`: Exclusive alternative path.
  - `nor`: Default / fallback path taken when no other conditions are satisfied.
  - `and`: Parallel simultaneous execution.
  - `or`: Inclusive path.
  - `sequence`: Standard unconditioned progression.

### 4. Backend Export Format
Clicking **Save & Export JSON** in the top navigation bar extracts a clean graph payload ready to be sent to your optimization backend:

```json
{
  "states": [
    {
      "id": "StartEvent_1",
      "name": "Start",
      "type": "bpmn:StartEvent",
      "time": "",
      "price": "",
      "Depends_on": [],
      "Connects_to": ["Activity_1"]
    },
    {
      "id": "Activity_1",
      "name": "Sample Task",
      "type": "bpmn:Task",
      "time": "15m",
      "price": "25 EUR",
      "Depends_on": ["StartEvent_1"],
      "Connects_to": ["EndEvent_1"]
    },
    {
      "id": "EndEvent_1",
      "name": "End",
      "type": "bpmn:EndEvent",
      "time": "",
      "price": "",
      "Depends_on": ["Activity_1"],
      "Connects_to": []
    }
  ],
  "branches": [
    {
      "id": "Flow_1",
      "name": "",
      "source": "StartEvent_1",
      "target": "Activity_1",
      "type": "sequence",
      "condition": null
    },
    {
      "id": "Flow_2",
      "name": "",
      "source": "Activity_1",
      "target": "EndEvent_1",
      "type": "sequence",
      "condition": null
    }
  ]
}
```
