# Gearbox Race Café — rFactor 2 Trackside — Implementation & Test Plan

Trackside is a planning-stage toolset for Gearbox Race Café's rFactor 2 simulator setup.

The first useful product is a live, near-real-time session board for spectators and staff. Camera direction, telemetry reports, PDF generation, printing, and incident replay remain part of the larger vision, but they are sequenced behind the live leaderboard work.

This document is written for two audiences:

* an implementation agent that can build code, tests, fixtures, docs, and repo-local tooling;
* the user/operator, who provides rFactor 2, Steam accounts, venue access, hardware, and live validation sessions.

The plan intentionally separates implementation work from setup/configuration work. An agent should not guess at venue details, Steam licensing, installed software, or hardware capability. If those inputs are unavailable, the agent should work from mocks, fixtures, and explicit spikes until the user provides the needed environment.

Trackside provides:

* Live session leaderboards and kiosk displays.
* Staff controls for driver aliases, session inclusion, and display modes.
* Optional spectator camera direction for one venue screen.
* Later per-driver telemetry web reports.
* Later PDF/printable telemetry reports.

Current planning status: Phase 0A PoC is complete, the telemetry/source direction is decided, ADR-0001 has accepted .NET / ASP.NET Core for the production host, Phase 0B has produced the service/tray/rig-agent/kiosk scaffold, and Phase 0C adds the packaged runtime skeleton before the leaderboard vertical slice.

Confirmed venue facts:

* 1 dedicated Windows server PC runs rFactor 2 Dedicated Server.
* 5 simulator rigs run as rFactor 2 clients.
* The 5 rigs already have Steam licenses and run F1 DLC cars/tracks.
* Rigs have full motion/input systems already implemented via rFactor 2 plugin(s); that work is out of scope.
* Server and rigs are on one wired LAN.
* Track, car, assists, session type, and session duration are managed server/host side. Rig users cannot change those settings locally.
* Current in-game driver names are fixed rig names such as `Setup1`, `Setup2`, etc.
* Multiple venue screens are available. Some should show web leaderboards; one may show a spectator camera feed.
* The screen-connected PC is relatively lean and should be treated as browser/kiosk hardware unless proven otherwise.
* No printer is currently available. Printing is a late nice-to-have, not an early dependency.

---

## 1. Planning Rules

### Ownership wording

Use a light ownership model rather than tagging every individual line:

* **Agent implementation** means code, tests, fixtures, parsers, UI, docs, local tooling, and bounded repo research.
* **User/operator prerequisite** means anything outside normal repo work: Steam accounts, paid DLC, installed rFactor 2, a running local server/client, credentials, machine access, hardware, printer setup, or business decisions.
* **Venue validation** means work that needs the real Gearbox Race Café machines, LAN, screens, plugins, or staff workflow.
* **Spike** means a bounded investigation with a written output and a decision or next action.

When a phase needs rFactor 2, the plan must word that as a prerequisite or validation input, not as an implementation task. For example, an agent can build a parser, fixtures, and a validation script; the user/operator provides the running rFactor 2 setup or captured data.

### Default approach for unknowns

* Investigate locally when the question can realistically be answered with docs, code, mocks, or a local rFactor 2 setup.
* Use a sensible default when the choice is low-risk and mostly obvious.
* Defer venue-specific open items until the relevant venue access or business decision is available.
* Do not leave hidden assumptions for a future implementation agent to guess. If something matters, make it a prerequisite, spike, or explicit default.

---

## 2. Stack And Tooling Direction

### Decisions already made

* Frontend/kiosk/admin UI: **React + Vite + TypeScript**.
* Backend host: **.NET / ASP.NET Core**, per `docs/architecture-decisions.md`.
* Live update style: **SignalR push updates** rather than slow polling.
* Storage default: **SQLite**.
* Camera plugin language: **C++**, when that phase is reached, because the rFactor 2 Internals Plugin SDK requires it.

### Backend stack decision

The backend stack decision is complete. `docs/architecture-decisions.md` records the accepted choice: **.NET / ASP.NET Core** for the production host, with Python retained only for diagnostics and optional offline/report workloads.

Phase 0B executed against that decision rather than reopening the stack comparison. The completed Phase 0A PoC informed the choice, but the temporary Python implementation is no longer the target production shape.

Current repository finding: the runnable production scaffold is now `services/trackside`, with `Trackside.Service`, `Trackside.Tray`, `Trackside.RigAgent`, `Trackside.Updater`, tests, fixture data, SignalR wiring, and Phase 0C packaging scripts. The Python Phase 0A PoC remains under `tools/rf2-poc` as reference evidence and diagnostics, and the rF2 shared-memory plugin snapshot remains under `vendor/rf2-shared-memory-map` as a dependency/reference.

---

## 3. Feasibility Summary

| Feature | Priority | Feasibility | Repo implementation? | Setup/venue dependency? |
| --- | ---: | --- | --- | --- |
| Core live-data browser PoC | Complete | Proven for current scope | Done; evidence in `docs/core-poc.md` | Revalidate on venue hardware during rollout |
| Live session leaderboard/kiosk | First | High | Yes: backend, fixtures, live model, UI, tests | Real rFactor 2 only needed for live validation |
| Staff aliases and display controls | First | High | Yes: admin UI/API/storage | Staff workflow details may need user confirmation |
| Historical daily/weekly/monthly boards | Secondary | High | Yes: storage/query/UI | Needs enough sample sessions to validate |
| Per-driver telemetry web reports | Later | Medium-high | Yes: capture/storage/charts/UI | Needs telemetry source validation |
| PDF reports / auto-print | Late nice-to-have | High once printer exists | Yes: PDF/print integration | Printer and print policy are user/venue prerequisites |
| Spectator camera feed/direction | Optional/later | Medium-high | Yes: C++ plugin if needed | Needs game-capable spectator setup and licensing decision |
| Incident detection / auto replay | Deferred | Medium | Yes: detection/replay control after camera exists | Needs live tuning sessions |

Overall: the core live-data loop is proven. The live leaderboard and staff controls are the best first product slice. They can be developed against mock scoring snapshots while real rFactor 2 data validates the parser and live update pipeline. Printing should stay late because no venue printer exists. Camera direction is feasible but depends on a game-capable spectator client or new hardware; the current lean display PC should not be assumed capable of running rFactor 2.

---

## 4. Product Requirements

### Live session board

The main screen is a live session overview, not just a static record board.

Required MVP behavior:

* Update near real-time so spectators can follow the session.
* Stay open on venue screens and automatically update across session transitions.
* Show a top summary with track name, session type, temperature, weather/conditions, and current session state when available.
* Show a table of current drivers/rigs.
* For practice and qualifying, order by fastest lap.
* For races, order by current race position/rank.
* Show display name, underlying rig/in-game name, best lap, current lap time, current sector times, and relevant gaps/position data when available.
* Use color coding for fastest lap/sector highlights.

Nice-to-have live behavior:

* Visual track map/track shape with live driver positions.
* Battle/overtake context, such as close gaps in race sessions.

### Historical leaderboard pages

Historical boards are secondary to the live session board.

Likely pages:

* Daily winners/bests.
* Weekly bests.
* Monthly bests.

Likely grouping dimensions, once data exists:

* Track.
* Car/content pack or F1 season.
* Session type.
* Assist preset/configuration.
* Date/time window.

### Driver identity

MVP identity behavior:

* rFactor 2 reports fixed names such as `Setup1`.
* Staff can assign a customer/display name to each rig for the current session.
* The leaderboard shows the staff-entered display name while retaining the underlying rig name for diagnostics.

Later investigation:

* Determine whether rFactor 2 player/name configuration can be updated safely on each setup so in-game names match customer names.
* Prove any name-sync approach on one non-critical local or venue machine before considering rollout.

### Staff/admin controls

Important MVP controls:

* Set driver aliases for each rig.
* Mark whether a session/result should count for historical boards.
* Exclude or correct a bad session/driver result after the fact.
* Choose which kiosk page/display mode a screen should show.

Later controls:

* Camera mode/pin/disable automation.
* Telemetry report generation.
* Print trigger once printing exists.

### Telemetry and reports

Telemetry is useful, but lower priority than the live board.

Preferred first telemetry version:

* Browser-based per-driver report page.
* Per-lap channels such as throttle, brake, steering, and gear.
* Personal/session comparison only after identity and storage are reliable.

PDF generation and printing should come after the web report. No printer exists today, so printing should be designed as a later deployment integration rather than an MVP dependency.

### Spectator camera

Multiple screens are available. The expected split is:

* several screens show live leaderboard/kiosk pages;
* one screen may show a spectator camera feed.

The existing display PC is probably not suitable for rFactor 2, so a camera feed likely requires either a game-capable spectator PC, another existing capable machine, or new hardware. Default eventual camera behavior can combine:

* rotating all drivers, to give customers screen time;
* following the leader;
* following close battles/action;
* trackside cinematic cameras;
* onboard/cockpit mix.

Do not attempt all camera modes in the first camera milestone. Start with a simple, predictable mode, then layer smarter behavior later.

Incident detection and automatic replay are deferred. They should be revisited only after the live board and basic camera feed are proven.

---

## 5. Architecture

The first architecture target is a venue-local web system fed by rFactor 2 timing/scoring data.

```text
[rF2 Dedicated Server on Windows]
            |
            | preferred authoritative timing/scoring source
            | validate shared-memory/scoring availability here first
            v
[rF2 shared-memory/scoring bridge]
            |
            v
[Trackside Service - .NET / ASP.NET Core]
            |-- SQLite storage
            |-- Shared-memory/scoring parser or adapter
            |-- Fixture/replay data adapter
            |-- Live timing/session API
            |-- SignalR live updates
            |-- Staff/admin API
            `-- Later telemetry report API

[Venue browser screens]
            |-- React/Vite public kiosk pages
            `-- React/Vite staff/admin routes

[Optional game-capable spectator client]
            `-- C++ camera director plugin ---> one spectator camera feed
```

Implementation rule: design the backend so its data source is replaceable. The first data source should be fixture/replay snapshots. The second data source should read real rFactor 2 shared-memory/scoring data once the user provides a local or venue environment.

Important data-source assumption: treat the dedicated server as authoritative for timing/results if technically possible. Phase 0A proved enough dedicated-server shared-memory scoring and telemetry coverage to start implementation; keep venue/live validation in the rollout path because exact field coverage and permissions can still vary by machine and rFactor 2 setup.

Fallbacks if server-side data is insufficient:

* read from a spectator client connected to the server;
* read from one selected sim rig for proof of concept;
* aggregate from all rigs only if a central source is not viable.

Telemetry implementation default: use dedicated-server telemetry first, with source-adapter modularity preserved for a future rig-local collector option. The decision details and rationale live in `docs/telemetry-report-poc-plan.md`.

The backend service host is a venue decision, not an implementation guess. Prefer the dedicated server PC if it has enough headroom and the venue accepts installing services there. Alternatives are the display/kiosk PC if suitable, or a small separate mini PC.

---

## 6. Human-Provided Environment Prerequisites

These are not implementation tasks for an AI coding agent. They are prerequisites or validation inputs the user/operator must provide.

### Local development prerequisites

* Provide a Windows machine where rFactor 2 can be installed and run, if live integration testing is desired.
* Provide or confirm a Steam account/license for one rFactor 2 client.
* Provide or confirm a working rFactor 2 Dedicated Server install, preferably via Steam App ID `400300`.
* Start a local rFactor 2 Dedicated Server and connect a local client with AI drivers when live testing is needed.
* Install or allow installation of the shared-memory/scoring bridge in the local rFactor 2 environment when live data validation is needed.
* Provide captured logs, screenshots, shared-memory snapshots, or remote access to the running local environment when the agent needs evidence from rFactor 2.

Suggested user-side install command for the dedicated server, kept here as guidance rather than an agent task:

```text
steamcmd +login anonymous +force_install_dir C:\rf2-dedicated +app_update 400300 +quit
```

### Venue prerequisites

* Confirm specs and resource headroom of the dedicated server PC and display/kiosk PC.
* Confirm admin rights and allowed installation locations on the server and rigs.
* Confirm existing rFactor 2 plugin inventory, especially motion/input plugins.
* Confirm whether a game-capable spectator PC exists or must be purchased.
* Confirm whether a spectator-only client needs F1 DLC ownership.
* Choose or install a printer only when the print phase becomes relevant.

### Agent-consumable artifacts

When rFactor 2 or venue access is not available, the agent should work from these artifacts instead of blocking:

* mock scoring/session snapshots;
* recorded scoring/session snapshots from a local or venue run;
* recorded telemetry snapshots for later report work;
* screenshots or videos of the venue display setup;
* notes about installed plugins, machine specs, and file locations.

---

## 7. Key Risks / Open Questions

| # | Risk / question | Resolution path | Blocking? |
| --- | --- | --- | --- |
| R1 | Can the shared-memory/scoring bridge expose enough data from the dedicated server? | Resolved by Phase 0A PoC for current scope; keep validating on venue hardware during rollout. | No for implementation start; validate again during venue deployment. |
| R2 | Where should backend services run at the venue? | Defer until venue specs are available; decide server PC vs display PC vs mini PC with a written deployment note. | Blocks venue deployment only. |
| R3 | Can a spectator-only client join without owning F1 car/track DLC? | Venue/user join test with a non-DLC account when camera work becomes active. | Blocks camera hardware/licensing only. |
| R4 | Is the existing display PC too lean for rFactor 2? | Venue/user spec check and optional spectator performance test. | Blocks camera feed only. |
| R5 | Can app-level aliases become in-game names later? | Local or venue spike against rFactor 2 player/profile config. | No, alias MVP can proceed. |
| R6 | Existing motion/input plugins could conflict with added plugins | Venue plugin inventory plus canary install before broad rollout. | Venue rollout risk. |
| R7 | Exact scoring/telemetry fields and struct versions may vary | Read shared-memory docs/headers and validate parser with fixtures/live snapshots. | No, handled during data spike. |
| R8 | rFactor 2/Steam auto-updates can break assumptions | User/venue update control and re-test procedure. | Ongoing operational risk. |
| R9 | Printer is not selected or installed | Defer printer selection and Windows print testing until print phase. | No, printing is late. |

None of these block starting the live board against mock data.

---

## 8. Phased Implementation & Test Plan

The phase lists below distinguish human prerequisites, agent implementation work, and validation. If a human prerequisite is missing, the agent should still complete mockable implementation tasks and mark live validation as pending.

### Phase 0A - Core live-data proof of concept

Status: complete.

Outcome:

* Dedicated-server shared memory is viable for live scoring/session data.
* Server-published telemetry is viable for telemetry report capture.
* Evidence and reproduction commands live in `docs/core-poc.md`.
* Telemetry-source policy lives in `docs/telemetry-report-poc-plan.md`.

### Phase 0B - Repository and architecture baseline

Status: complete for architecture/scaffold baseline. Live rFactor 2 parser validation remains pending until a real rFactor 2 run or captured memory-map data is available. Memory-map reading, SQLite persistence, staff controls, and the real scoring board remain future work.

Human prerequisites:

* Review or reopen ADR-0001 only if the accepted .NET / ASP.NET Core direction is being challenged.
* Provide local rFactor 2 access only if live parser validation is expected during Phase 0B. Otherwise Phase 0B proceeds with mocks.

Agent implementation tasks:

* Keep the monorepo structure: `/plugin`, `/services`, `/web`, `/docs`, `/tools`, and `/vendor`.
* Use ADR-0001 as the implementation baseline: .NET 10 / ASP.NET Core 10, C# 14, SignalR, SQLite, and Node.js only for React/Vite tooling.
* Create the backend project layout using the selected stack. - Done for Phase 0B scaffold.
* Create the React/Vite/TypeScript kiosk/admin app layout in `web/kiosk`. - Done for basic kiosk shell.
* Add a documented mock scoring/session snapshot format. - Done as a normalized fixture contract.
* Add fixture loading in the backend so the UI can run without rFactor 2. - Done.
* Add basic build/test commands to the relevant READMEs. - Done for `services/trackside` and `web/kiosk`.
* Port the PoC shared-memory map-name, wrapper-offset, zero-offset, and field-mapping evidence into production parser tests. - Parser-offset structure is in place; full field mapping remains future shared-memory work.
* Inspect `rF2SharedMemoryMapPlugin` docs/headers/examples as needed to list any remaining required scoring fields for Phase 1. - Existing PoC evidence is enough for fixture-first Phase 1; live parser field validation remains pending.

Validation:

* Backend starts from mock fixtures. - Done.
* Kiosk app displays a mock live session table. - Done as a basic shell.
* README commands reproduce the local mock run. - Done.
* Parser tests cover wrapper-offset and zero-offset shared-memory payloads. - Done.

### Phase 0C - Packaged runtime and update skeleton

Status: scaffolding target before Phase 1. This phase establishes the packaged runtime shape without building a full installer, remote update host, code-signing pipeline, or silent update flow.

Human prerequisites:

* Provide admin rights only when installing as a real Windows Service is being tested. Dry-run and bundle smoke tests do not require elevation.
* Choose final venue install locations later; the scripts default to `C:\Program Files\Gearbox Trackside` for service installs and `artifacts\trackside\bundles` for repo-local bundles.

Agent implementation tasks:

* Add a repeatable package script that builds/tests the .NET solution, builds the kiosk, publishes `Trackside.Service`, `Trackside.Tray`, `Trackside.RigAgent`, and `Trackside.Updater`, and writes a versioned bundle.
* Keep bundle layout explicit: read-only app files, editable config, durable data, logs, and update staging are separate paths.
* Add PowerShell install/uninstall scripts for `Trackside.Service` as a Windows Service and current-user tray auto-start.
* Add manifest schema version 1 with app version, bundle version, minimum compatible version, entry points, file list, SHA-256 checksums, and byte lengths.
* Add a tiny out-of-process `Trackside.Updater` boundary for manifest inspect/verify/plan commands; do not make `Trackside.Service` replace its own files.
* Extend `/api/health` with version, install mode, service state, layout paths, manifest path, and update placeholder fields.
* Add dry-run and fixture-backed bundle smoke tests.
* Document the commands and limits in `docs/deployment-skeleton.md`.

Validation:

* `dotnet test services\trackside\Trackside.slnx` passes.
* PowerShell scripts parse successfully.
* The package script creates a versioned bundle and manifest.
* `Trackside.Updater verify` accepts the generated manifest.
* The bundle smoke test starts the packaged service in fixture mode, checks `/api/health`, and fetches `/api/live-session/current`.

### Phase 1 - Live session leaderboard MVP

Status: Phase 1 live leaderboard foundation is implemented and live-validated for the current leaderboard scope. Fixture-first behavior, raw scoring fixtures, normalized builder, practice/qualifying/race ordering, fastest lap/sector highlights, config-backed aliases, kiosk live board, reconnect/current-snapshot tests, shared-memory scoring loop/parser scaffolding, PID/map auto-discovery, reload-aware source selection, tray status colors, and the authenticated admin/source configuration dashboard are in place. Live validation on the local PC confirmed shared-memory autodiscovery, tray red/blue/green status, live board updates, flag state, temperatures, session type, track name, and displayed leaderboard fields.

Human prerequisites:

* Provide representative mock data if real rFactor 2 data is not available yet.
* Local live validation has been provided for the current shared-memory leaderboard fields. Future validation is still needed when venue hardware, plugin versions, or rFactor 2 configuration differ.

Agent implementation tasks:

* Build the normalized live session model: session type, track, weather/temperature, drivers/rigs, laps, sectors, best laps, current laps, race position, and gaps where available. - Done for current leaderboard fields; normalized scoring fields extracted by Phase 1 are exposed to the browser contract.
* Build a fixture-backed scoring/session source. - Done for raw scoring-style leaderboard fixtures.
* Build a shared-memory-backed scoring/session source behind the same interface once field layout is known, guarded so the app still runs without rFactor 2. - Done for current live leaderboard scope: scoring map reader, parser, dedicated polling loop, process-name/Section-object auto-discovery, exact map/PID override config, multiple-map ambiguity handling, stale-read clearing, and scoring update-counter stability checks are implemented and live-validated.
* Build the SignalR live update flow from backend to browser. - Existing Phase 0B flow retained and covered by current-snapshot recovery tests.
* Build the public kiosk page with session summary and live table. - Done for the current live-board table; layout/graphics can be refined later.
* Sort practice/quali by fastest lap and race by current position. - Done fixture-first.
* Add fastest-lap/sector highlighting. - Done fixture-first.
* Add staff-entered aliases mapping fixed rig names such as `Setup1` and `Setup2` to customer display names. - Started with config-backed aliases editable through the admin dashboard and applied through reload-aware source configuration.
* Ensure kiosk pages can stay open and update automatically across session changes. - Done for current validation: REST recovery plus SignalR feed startup are in place, and the service-hosted fallback refreshes current snapshots.

Validation:

* Automated fixture tests cover practice, qualifying, and race ordering. - Done.
* Automated tests cover alias mapping and fastest-lap/sector highlighting. - Done.
* Live rFactor 2 validation updates the kiosk near real-time. - Done for the local PC/current displayed fields.
* Live validation confirmed autodiscovery, tray icon status colors, live board updates, flag state, temperatures, session type, track name, and displayed leaderboard fields.

Deferred from Phase 1, but still tracked:

* High-cadence shared-memory robustness belongs with telemetry/report work in Phase 5, where the polling rate and data-loss consequences are different from low-cadence live timing.
* Telemetry channels remain Phase 5; Phase 1 intentionally uses scoring/session channels only.
* Historical persistence, SQLite/database storage, result summaries, and daily/weekly/monthly boards remain Phase 2.
* The proper staff alias workflow, session inclusion/exclusion, corrections, and kiosk display-mode controls remain Phase 2. Phase 1 only has config-backed aliases.
* Venue rollout hardening, runbooks, final host/install decisions, canary rollout, restart/recovery checks, update flow, and rollback validation remain Phases 3 and 7.
* More polished kiosk layout/graphics remain a follow-up UI task after the live data contract is stable; the current Phase 1 board is functional.
* Captured raw memory-map regression fixtures are useful hardening and should be added when convenient, but they no longer block Phase 1 because live validation has proven the current leaderboard path.

### Phase 2 - Staff controls and historical boards

Current admin/security baseline: the service has cookie-based admin login, local file-backed admin accounts with salted PBKDF2-HMAC-SHA256 password hashes, installer-first initial admin bootstrap, first-run web setup fallback when no admin users exist, protected source configuration, protected admin user management, and protected advanced status. The public kiosk and basic health endpoint remain unauthenticated; detailed paths, source diagnostics, discovery candidates, and admin user status are admin-only. Config and admin-store writes use temp-file replacement; packaged installs restrict the admin-store ACL to SYSTEM and Administrators.

Human prerequisites:

* Confirm staff workflow assumptions: who sets aliases, when aliases reset, and whether historical boards count all sessions by default or require opt-in.

Agent implementation tasks:

* Add SQLite persistence for sessions, participants, laps, sectors, aliases, and summary results.
* Add staff/admin UI for aliases, session inclusion/exclusion, and kiosk display mode.
* Add correction/exclusion flows for bad sessions or incorrect driver mappings.
* Add secondary daily/weekly/monthly best pages.
* Group historical results by track, car/content, session type, assist preset if available, and date window.

Validation:

* Staff can assign aliases, mark whether a session counts, correct/exclude results, and view current plus historical boards using fixture data.
* Persistence survives backend restart.

### Phase 3 - Venue data-source and deployment validation

Human prerequisites:

* Provide physical or remote access during an agreed venue validation window.
* Confirm admin rights and acceptable install locations.
* Provide machine specs for the server PC and display PC.

Agent support tasks:

* Prepare an install checklist for the shared-memory bridge and Trackside service.
* Prepare a rollback checklist.
* Prepare a validation checklist for field coverage, service startup, kiosk connectivity, and restart behavior.

Venue validation:

* Confirm whether the shared-memory/scoring bridge works on the dedicated server PC and exposes all required live board fields.
* If server-side data is insufficient, test fallback sources: spectator client, one rig, or all rigs.
* Confirm a suitable service host: dedicated server PC preferred, display PC or separate mini PC as fallback.
* Validate coexistence with existing motion/input plugins on one canary rig before broad installation.

Exit criteria:

* Written decision for venue data source, service host, install path, rollback path, and canary rollout order.

### Phase 4 - Spectator camera feasibility and v1

Human prerequisites:

* Confirm whether a game-capable spectator PC/license/content is available or must be purchased.
* Run or support the test for spectator-only DLC requirements.

Agent implementation tasks:

* Only after hardware/licensing is viable, scaffold the C++ camera plugin project.
* Read the Studio 397 Internals Plugin SDK and document the minimum callbacks needed for camera control.
* Implement one simple camera mode first: rotate all drivers or follow leader.
* Add optional staff override through a simple external control file or existing admin UI.
* Keep all plugin logic defensive and narrow; no storage, PDF generation, or web server inside the plugin.

Validation:

* One spectator camera feed can run without disturbing the live board or simulator clients.

### Phase 5 - Telemetry web report MVP

The current telemetry-report proof-of-concept findings and final source decision live in `docs/telemetry-report-poc-plan.md`. `docs/core-poc.md` records the source-preservation evidence from the completed Phase 0A PoC.

Human prerequisites:

* Confirm telemetry report priority before this phase starts.
* Provide local or recorded telemetry snapshots if live rFactor 2 access is not available.

Agent implementation tasks:

* Implement central server-side telemetry capture as default mode.
* Use a dedicated high-rate telemetry loop that polls at `100 Hz` by default, with lower-rate scoring/session reads.
* Keep report generation and driver-statistics pipelines source-agnostic through a normalized telemetry ingestion interface.
* Add configuration-driven telemetry source selection so rig-local collectors can be enabled later without redesigning downstream report/analysis code.
* Capture or parse per-driver/per-lap channels such as throttle, brake, steering, and gear.
* Store telemetry with enough identity/session metadata to associate it with aliases and historical laps.
* Build a browser-based per-driver report page.
* Add comparison against session best or personal best once identity and storage are reliable.
* Suppress or adapt gear display when auto-shift/assist configuration says gear analysis is not meaningful.

Validation:

* Recorded telemetry fixtures render a usable per-driver web report.
* Local rFactor 2 laps, when provided, produce a usable per-driver telemetry page without PDF or printer dependency.

### Phase 6 - Late nice-to-haves: PDF, printing, incident replay

Human prerequisites:

* Choose or install a venue printer before implementing print-specific behavior.
* Confirm whether staff actually want automatic printing or only manual print/download.
* Confirm whether incident replay is operationally desirable after camera v1 is proven.

Agent implementation tasks:

* Add branded PDF generation after the web report is stable.
* Add manual print/download first; auto-print only after staff approves the workflow.
* Add manual replay trigger before automatic incident detection.
* For automatic incident replay, confirm exact telemetry/rules fields for impact/off-track/spin detection and tune against local sessions.

Validation:

* Print path works on the real venue printer.
* Replay features can be triggered deliberately and disabled quickly if disruptive.

### Phase 7 - Venue rollout, hardening, training, handover

Human prerequisites:

* Approve rollout window and canary machine/screen.
* Confirm who will operate the system during opening hours.

Agent implementation/support tasks:

* Use the Phase 0C bundle/install skeleton for canary releases and rollback.
* Configure final service auto-start/restart settings for the chosen venue service host.
* Package the tray companion/status surface so it auto-starts for the venue user account when appropriate and can reopen dashboards after reboot.
* Add rotating log files for backend services and any plugin control surface.
* Add manual disable/override switches for every automated feature.
* Implement dashboard-visible update checks against a signed/versioned manifest hosted on an operator-controlled server.
* Extend the Phase 0C updater boundary into a staff-approved update flow: download bundle, verify version/signature/checksum, stop affected services, swap files with rollback, restart services, and report success/failure.
* Prevent silent updates during active sessions; require an idle/safe restart window.
* Write a short non-technical runbook for venue staff.
* Document rFactor 2/Steam update control and re-test procedure.

Venue validation:

* Roll out as a canary first: one service host, one display screen, one rig/client path if applicable.
* Run a real venue session end-to-end with live board, aliases, staff controls, and agreed optional features.
* Reboot the service host and confirm the service recovers, the tray/status surface appears for the venue user, and dashboards can be reopened without developer help.
* Test update installation and rollback with a harmless canary build before allowing venue-facing updates.

---

## 9. Agent-Ready Next Work Package

If an implementation agent starts from this document, begin with the post-PoC baseline:

1. Optionally capture a small set of validated scoring snapshots for regression fixtures once convenient; this is useful hardening but no longer blocks Phase 1 behavior.
2. Replace config-backed aliases with the first staff workflow when Phase 2 starts.
3. Start Phase 2 storage design: SQLite-backed stores behind application-level interfaces for aliases, sessions, laps/sectors, and historical summaries.
4. Keep shaping the kiosk as a layout layer over the normalized snapshot, keeping data/feed logic outside presentation components.
5. Carry forward the telemetry source requirements from `docs/telemetry-report-poc-plan.md` when report work begins; high-cadence shared-memory robustness belongs with telemetry/report work, not the current low-cadence live timing board.

Do not restart Phase 0A, Steam setup, venue setup, printer setup, or camera plugin work unless the user explicitly provides those prerequisites and asks for that phase.

---

## 10. Engineering Recommendations

**Repo structure**: keep the single monorepo: `/plugin`, `/services`, `/web`, `/docs`, `/tools`, and `/vendor`. The project is small and tightly coupled enough that a split would add friction now.

**Dependency policy**:

* Reuse `rF2SharedMemoryMapPlugin` for telemetry/scoring export unless venue validation reveals a new blocker.
* Use the official Studio 397 Internals Plugin SDK directly for camera work.
* Keep game-process plugins narrow and defensive.
* Do not put storage, PDF generation, network servers, or complex parsing inside the game plugin.
* Do not commit Steam credentials, venue credentials, printer credentials, or personal account identifiers.

**Deployment strategy**:

* Prefer a central venue-local service host on the wired LAN.
* Prefer the dedicated server PC only if resource usage and operational risk are acceptable.
* Keep display PCs as browser/kiosk clients where possible.
* Maintain local fixture-based tests even after real rFactor 2 integration exists.
* Roll out to the venue as a canary before touching all machines.

**Stability practices**:

* The live board must fail independently of the race. A service/UI crash must not affect rFactor 2.
* Staff should always have a manual way to correct aliases, exclude a bad result, or change display mode.
* Camera automation and printing must have obvious disable paths before venue rollout.
* Add rotating logs early enough that venue debugging does not depend on a developer watching a console.

**Process/maintenance**:

* Use lightweight issue tracking.
* Add CI once there is executable code: backend tests, frontend tests/lint, then Windows plugin build later.
* Keep two tiers of docs: developer docs here and a short venue-staff runbook later.

---

## 11. Reference Materials

* Studio 397 Internals Plugin SDK - official, free, distributed via Studio 397's modding resources/forum.
* `rF2SharedMemoryMapPlugin` (TheIronWolfModding) - open-source telemetry/scoring shared-memory bridge, widely used by third-party rF2 tools.
* SteamCMD - used for the anonymous, account-free install of the Dedicated Server (App ID `400300`).
* rFactor 2 client - Steam App ID `365960`.
* rFactor 2 Dedicated Server - Steam App ID `400300`.
