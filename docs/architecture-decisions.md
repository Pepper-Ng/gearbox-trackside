# Architecture Decisions

## ADR-0001: Backend stack for Phase 0B

**Status:** Accepted  
**Date:** 2026-06-22

### Decision

The production backend will be built on **.NET / ASP.NET Core**.

Trackside will ship as one Windows-oriented application bundle whose primary always-on process is a **.NET Trackside Host**. That host owns shared-memory reading, scoring, persistence, local HTTP hosting, push updates, configuration, diagnostics, and orchestration of optional worker processes.

The default product shape is:

```text
[rFactor 2 / Dedicated.exe + rF2SharedMemoryMapPlugin]
        |
        | Windows named shared-memory maps
        v
[Trackside Host - .NET / ASP.NET Core]
        |-- scoring collector worker
        |-- telemetry collector worker, when telemetry is enabled
        |-- normalized live-session model
        |-- SQLite storage
        |-- SignalR live update hub
        |-- local API and static web hosting
        |-- optional Python report jobs, not in the live timing path
        v
[React kiosk/admin web UI in browser]
```

Node.js is rejected for the backend. It can remain part of the frontend toolchain, but it will not own shared-memory parsing or timing-critical collection.

Python is retained as a useful tool, but not as the main backend runtime. The existing Python PoC remains valuable for diagnostics and for proving field mappings. Later report generation may use Python sidecar jobs for charts, analysis, or PDFs if the Python ecosystem clearly speeds that work up. Those jobs must be launched and supervised by the .NET host and must not sit in the 50 Hz collection or live scoring path.

### Why this is the decision

The product's hardest requirements are Windows reliability, shared-memory parsing, local process orchestration, predictable background operation, and live push updates. ASP.NET Core and .NET fit those requirements directly:

* native support for Windows services, single-file deployment, structured logging, background workers, and health endpoints;
* first-class binary parsing, P/Invoke, and `MemoryMappedFile` support for the rFactor 2 shared-memory plugin;
* SignalR for browser push updates without inventing a custom WebSocket protocol;
* good SQLite support through `Microsoft.Data.Sqlite`;
* enough performance headroom to keep data collection independent from UI publishing.

The Python PoC proved the shared-memory path and browser concept, but it also exposed the wrong production shape: one Python process doing collection, scoring, and web serving is too easy to overload. Splitting Python workers might work, but it makes the critical path depend on more process coordination while still leaving Windows service/tray integration as extra work. The production backend should start from the Windows-native host instead.

### Runtime versions

Use these versions for new Phase 0B scaffolding:

| Area | Version |
| --- | --- |
| .NET SDK/runtime | **.NET 10 LTS**, latest 10.0.x patch |
| ASP.NET Core | **ASP.NET Core 10** |
| C# | **C# 14** |
| SQLite library | `Microsoft.Data.Sqlite` 10.x |
| Frontend runtime | **Node.js 24 LTS**, used only for React/Vite tooling |
| Optional Python jobs | **Python 3.12** unless a later report package requires newer |

The repo should add a `global.json` when the .NET solution is scaffolded so contributors build with the selected SDK family.

### Package manager choice

* Backend: **NuGet through the `dotnet` CLI**. Use checked-in project files and lock dependencies once the project is created.
* Frontend: **npm** for the React/Vite app, because the frontend is small and npm is the lowest-friction default.
* Python: no production Python package manager in Phase 0B. If Python report workers are added later, use a locked environment and keep it isolated from the live host.

### Local run commands

Phase 0B backend scaffolding must implement these commands from the repository root:

```powershell
dotnet run --project services\trackside\Trackside.Host -- --source fixture --fixture services\leaderboard\poc\fixtures\mock_scoring_snapshot.json
dotnet run --project services\trackside\Trackside.Host -- --source shared-memory --pid <Dedicated.exe PID>
```

The host should bind to localhost by default, for example:

```text
http://127.0.0.1:8877
```

The existing Phase 0A Python PoC remains runnable for comparison and diagnostics:

```powershell
python services\leaderboard\poc\run_poc.py --source mock
python services\leaderboard\poc\run_poc.py --source shared-memory --pid <Dedicated.exe PID>
```

Frontend development should use Vite against the .NET API during development and the .NET host should serve the built frontend for packaged/local operation:

```powershell
npm --prefix web\kiosk install
npm --prefix web\kiosk run dev
```

### Test commands

Phase 0B scaffolding must implement:

```powershell
dotnet test services\trackside\Trackside.sln
npm --prefix web\kiosk test
```

Keep the current PoC tests available while the .NET reader is being built:

```powershell
python -m unittest discover services\leaderboard\poc\tests
```

### Live update decision

The backend will expose live browser updates through **SignalR**.

Required endpoints:

| Endpoint | Purpose |
| --- | --- |
| `/hubs/live-session` | Push normalized live-session snapshots to kiosk/admin clients. |
| `/api/live-session/current` | Return the latest snapshot for initial load, reconnect recovery, and debugging. |
| `/api/health` | Return host health, source status, and version information. |

The scoring collector may read more often internally, but browser publish cadence should be configurable and capped to the live board need, initially **1-10 Hz**. Telemetry collection at **50 Hz** must not push every raw sample to kiosk clients. Telemetry should be collected, buffered, persisted, and summarized separately so a slow browser cannot slow down data collection.

SignalR reconnects must recover by fetching `/api/live-session/current` and then resubscribing. The UI should never depend on page refreshes for normal session transitions.

### Data-source abstraction decision

The backend will use explicit source interfaces so mock data and real rFactor 2 data share one normalized model:

```text
ILiveSessionSource
  FixtureLiveSessionSource
  RecordedSnapshotLiveSessionSource
  Rf2SharedMemoryLiveSessionSource

ITelemetrySource
  DisabledTelemetrySource
  FixtureTelemetrySource
  Rf2SharedMemoryTelemetrySource
```

The source is selected by configuration and CLI arguments:

```powershell
--source fixture
--source shared-memory --pid <Dedicated.exe PID>
--source recorded --path <snapshot-folder>
```

The normalized model, storage, SignalR hub, and UI must not know whether a snapshot came from fixture JSON, recorded snapshots, or live shared memory. This keeps development unblocked without rFactor 2 while preserving the real integration path.

The .NET shared-memory reader should be implemented from the same evidence used by the Python PoC:

* scoring map names include `$rFactor2SMMP_Scoring$`, `$rFactor2SMMP_Scoring$<PID>`, and `Global\$rFactor2SMMP_Scoring$<PID>`;
* the reader must fail explicitly when a named map cannot be opened;
* parser tests must include wrapper-offset and zero-offset payload cases;
* live parser validation remains pending until a real rFactor 2 run is available.

### Process and deployment decision

The reliable venue shape is a **service plus control surface**, not a web server that staff have to babysit.

Decision:

* The primary runtime is `Trackside.Host`, a .NET process designed to run unattended and auto-restart.
* For early development it may run as a console app.
* For venue rollout it should run as a Windows service or service-like background process.
* A tray icon/control app may be added as a thin .NET companion, but it must not be required for collection, scoring, storage, or kiosk updates.
* Configuration, status, diagnostics, and staff controls should be local web pages hosted by `Trackside.Host`; the tray menu should mostly open those pages and expose obvious start/stop/status actions.

This keeps the user-facing product as one installed Trackside application while avoiding the Windows limitation that true services cannot directly own an interactive tray icon in the user's desktop session.

### Storage decision

Use **SQLite** as planned.

Initial rules:

* Keep current live state in memory and publish it through SignalR.
* Persist sessions, participants, laps, aliases, and summary results to SQLite.
* Use WAL mode for better read/write behavior.
* Batch telemetry writes; do not write one SQLite transaction per 50 Hz sample.
* Start with `Microsoft.Data.Sqlite` and explicit SQL/migrations. Do not put ORM convenience in the hot telemetry path.

### Worker isolation decision

Collection must not be blocked by rendering, reporting, browser clients, or slow storage.

The .NET host should use separate hosted workers and bounded channels:

* scoring collection worker for live session/scoring data;
* telemetry collection worker for 50 Hz telemetry once that phase is enabled;
* persistence worker for batched database writes;
* SignalR publisher worker for browser-facing snapshots;
* optional report job runner for slower Python or .NET report generation.

If live validation shows that one .NET process still cannot keep telemetry and web publishing independent enough, the next split is **separate .NET worker processes supervised by Trackside.Host**, not a switch back to Python as the main backend.

### Consequences

Immediate Phase 0B work should scaffold a new .NET backend under `services\trackside` and keep the Python PoC under `services\leaderboard\poc` as a reference and validation tool.

The architecture deliberately chooses reliability over having one language everywhere. The frontend remains TypeScript/React, the live backend is .NET, and Python is reserved for diagnostics and optional offline/report workloads where it is strongest.
