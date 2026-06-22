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

Current planning status: exploratory planning. The goal is to refine enough detail to start development locally, then validate against the venue when access is available.

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
* Live update style: **push-based updates**, using WebSockets or SignalR-style behavior rather than slow polling.
* Storage default: **SQLite**, unless a stack spike finds a strong reason otherwise.
* Camera plugin language: **C++**, when that phase is reached, because the rFactor 2 Internals Plugin SDK requires it.

### Backend stack spike

The backend stack is not finalized yet. Phase 0B must run a short stack spike and choose one backend stack before full feature implementation begins. Phase 0A may use the quickest reasonable temporary implementation to prove the live rFactor 2-to-browser data path; that PoC should inform the stack decision rather than block on a perfect architecture.

Candidate stacks:

* **.NET / ASP.NET Core**: strong Windows service hosting, SignalR, typed binary parsing, memory-mapped file support, SQLite support.
* **Python / FastAPI**: fast iteration, strong data/reporting ecosystem for later telemetry charts and PDFs.
* **TypeScript / Node**: one language across backend and frontend, good web tooling, but less obviously ideal for binary/shared-memory parsing on Windows.

Spike output must be a short decision record in `docs/architecture-decisions.md` that includes:

* chosen backend stack and why;
* selected runtime versions;
* package manager choice;
* local run commands;
* test commands;
* how the backend will expose push updates to the React kiosk;
* how the backend will abstract mock data versus real rFactor 2 data.

The spike should prefer a concrete decision over prolonged comparison. If no strong evidence appears, default to .NET / ASP.NET Core because the deployment target is Windows and shared-memory parsing is central to the project.

---

## 3. Feasibility Summary

| Feature | Priority | Feasibility | Repo implementation? | Setup/venue dependency? |
| --- | ---: | --- | --- | --- |
| Core live-data browser PoC | First | High if shared-memory data is available locally | Yes: one minimal backend command and one simple browser page | User must provide local rFactor 2 session for live proof |
| Live session leaderboard/kiosk | First | High | Yes: backend, fixtures, live model, UI, tests | Real rFactor 2 only needed for live validation |
| Staff aliases and display controls | First | High | Yes: admin UI/API/storage | Staff workflow details may need user confirmation |
| Historical daily/weekly/monthly boards | Secondary | High | Yes: storage/query/UI | Needs enough sample sessions to validate |
| Per-driver telemetry web reports | Later | Medium-high | Yes: capture/storage/charts/UI | Needs telemetry source validation |
| PDF reports / auto-print | Late nice-to-have | High once printer exists | Yes: PDF/print integration | Printer and print policy are user/venue prerequisites |
| Spectator camera feed/direction | Optional/later | Medium-high | Yes: C++ plugin if needed | Needs game-capable spectator setup and licensing decision |
| Incident detection / auto replay | Deferred | Medium | Yes: detection/replay control after camera exists | Needs live tuning sessions |

Overall: the first thing to prove is the core live-data loop: rFactor 2 session/player data reaches a browser with minimal moving parts. After that, the live leaderboard and staff controls are the best first product slice. They can be developed against mock scoring snapshots before rFactor 2 is available, while real rFactor 2 data validates the parser and live update pipeline. Printing should stay late because no venue printer exists. Camera direction is feasible but depends on a game-capable spectator client or new hardware; the current lean display PC should not be assumed capable of running rFactor 2.

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
[Trackside backend service - stack chosen in Phase 0B]
            |-- SQLite storage
            |-- Shared-memory/scoring parser or adapter
            |-- Fixture/replay data adapter
            |-- Live timing/session API
            |-- WebSocket/SignalR-style live updates
            |-- Staff/admin API
            `-- Later telemetry report API

[Venue browser screens]
            |-- React/Vite public kiosk pages
            `-- React/Vite staff/admin routes

[Optional game-capable spectator client]
            `-- C++ camera director plugin ---> one spectator camera feed
```

Implementation rule: design the backend so its data source is replaceable. The first data source should be fixture/replay snapshots. The second data source should read real rFactor 2 shared-memory/scoring data once the user provides a local or venue environment.

Important data-source assumption: treat the dedicated server as authoritative for timing/results if technically possible. The data-source spike must prove whether the shared-memory/scoring bridge exposes the needed session, lap, sector, position, weather, and participant data from the dedicated server process.

Fallbacks if server-side data is insufficient:

* read from a spectator client connected to the server;
* read from one selected sim rig for proof of concept;
* aggregate from all rigs only if a central source is not viable.

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
| R1 | Can the shared-memory/scoring bridge expose enough data from the dedicated server? | Spike locally when rFactor 2 is supplied; record source process, fields present/missing, and chosen adapter path. | Blocks final data architecture, not mock UI work. |
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

Purpose: answer the central feasibility question as quickly as possible: can rFactor 2 session/player data be extracted from the shared-memory map or equivalent local source and shown in a browser with minimal software?

This phase is intentionally smaller than the full leaderboard MVP. It should not include polished UI, historical storage, staff controls, aliases, printing, telemetry reports, or camera work.

Human prerequisites:

* Provide a local Windows rFactor 2 Dedicated Server and client session if live validation is expected.
* Start a simple session with AI drivers connected so there is changing session/player data.
* Install or allow use of the shared-memory/scoring bridge, or provide captured shared-memory/scoring snapshots if live access is not available.
* Confirm the local URL/port can be opened in a browser on the same machine.

Agent implementation tasks:

* Build the smallest runnable backend command or executable that can read one scoring/session source.
* Prefer reading live shared-memory/scoring data if the user provides a running local rFactor 2 setup; otherwise implement the same shape against captured or synthetic snapshots.
* Expose a single local HTTP page such as `/poc`.
* Display only enough data to prove the core idea: session name/type if available, track if available, current drivers/player names, position/order if available, current/best lap if available, and a visible timestamp/update counter.
* Update the browser automatically using the simplest push/poll method available in the chosen spike implementation.
* Add a short `docs/core-poc.md` note explaining how to run the PoC, what source was used, which fields appeared, which fields were missing, and whether the result supports continuing to the full leaderboard.

Validation:

* One command starts the backend/PoC server.
* One browser page shows live or replayed rFactor 2 session/player data.
* When AI drivers join or session data changes, the browser visibly updates without refresh.
* The PoC records a clear go/no-go decision for the full live leaderboard path.

### Phase 0B - Repository and architecture baseline

Human prerequisites:

* Review the backend stack spike result before feature implementation continues.
* Provide local rFactor 2 access only if live parser validation is expected during Phase 0B. Otherwise Phase 0B proceeds with mocks.

Agent implementation tasks:

* Keep the monorepo structure: `/plugin`, `/services`, `/web`, `/docs`.
* Run the backend stack spike and write `docs/architecture-decisions.md`.
* Create the backend project layout using the selected stack.
* Create the React/Vite/TypeScript kiosk/admin app layout in `web/kiosk`.
* Add a documented mock scoring/session snapshot format.
* Add fixture loading in the backend so the UI can run without rFactor 2.
* Add basic build/test commands to the relevant READMEs.
* Inspect `rF2SharedMemoryMapPlugin` docs/headers/examples and list required scoring fields for Phase 1.

Validation:

* Backend starts from mock fixtures.
* Kiosk app displays a mock live session table.
* README commands reproduce the local mock run.
* Stack decision is documented with runtime versions, commands, and rationale.

### Phase 1 - Live session leaderboard MVP

Human prerequisites:

* Provide representative mock data if real rFactor 2 data is not available yet.
* When ready for live validation, provide access to a local rFactor 2 client/server session or captured shared-memory snapshots.

Agent implementation tasks:

* Build the normalized live session model: session type, track, weather/temperature, drivers/rigs, laps, sectors, best laps, current laps, race position, and gaps where available.
* Build a fixture-backed scoring/session source.
* Build a shared-memory-backed scoring/session source behind the same interface once field layout is known, guarded so the app still runs without rFactor 2.
* Build WebSocket/SignalR-style live update flow from backend to browser.
* Build the public kiosk page with session summary and live table.
* Sort practice/quali by fastest lap and race by current position.
* Add fastest-lap/sector highlighting.
* Add staff-entered aliases mapping fixed rig names such as `Setup1` and `Setup2` to customer display names.
* Ensure kiosk pages can stay open and update automatically across session changes.

Validation:

* Automated fixture tests cover practice, qualifying, and race ordering.
* Automated tests cover alias mapping and fastest-lap/sector highlighting.
* Live rFactor 2 AI session, when provided, updates the kiosk near real-time.

### Phase 2 - Staff controls and historical boards

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

Human prerequisites:

* Confirm telemetry report priority before this phase starts.
* Provide local or recorded telemetry snapshots if live rFactor 2 access is not available.

Agent implementation tasks:

* Confirm whether telemetry is best captured from server, spectator client, or each rig using documented evidence.
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

* Package releases as versioned file-based bundles so rollback is simple.
* Configure services to auto-start and auto-restart on Windows, using the chosen service hosting approach.
* Add rotating log files for backend services and any plugin control surface.
* Add manual disable/override switches for every automated feature.
* Write a short non-technical runbook for venue staff.
* Document rFactor 2/Steam update control and re-test procedure.

Venue validation:

* Roll out as a canary first: one service host, one display screen, one rig/client path if applicable.
* Run a real venue session end-to-end with live board, aliases, staff controls, and agreed optional features.
* Reboot the service host and confirm the system recovers.

---

## 9. Agent-Ready First Work Package

If an implementation agent starts from this document, begin here:

1. Start with Phase 0A if the user can provide a local rFactor 2 server/client session: build the smallest command/browser PoC that shows live or captured session/player data.
2. Write `docs/core-poc.md` with the run command, data source, visible fields, missing fields, and go/no-go decision for the full leaderboard path.
3. If local rFactor 2 is not available yet, build the same browser shape against replayed fixture snapshots and mark live validation pending.
4. Run the backend stack spike and write `docs/architecture-decisions.md`.
5. Create backend and React/Vite kiosk/admin project scaffolding using the selected stack.
6. Define the mock live session snapshot schema with practice, qualifying, and race examples.
7. Serve fixture-backed live session data from the backend.
8. Render the public kiosk table and session summary from fixture data.
9. Add automated tests for session sorting and alias mapping.
10. Document how the user can later provide local rFactor 2 snapshots for parser validation.

Do not start with Steam, venue setup, printer setup, or camera plugin work unless the user explicitly provides those prerequisites and asks for that phase.

---

## 10. Engineering Recommendations

**Repo structure**: keep the single monorepo: `/plugin`, `/services`, `/web`, `/docs`. The project is small and tightly coupled enough that a split would add friction now.

**Dependency policy**:

* Reuse `rF2SharedMemoryMapPlugin` for telemetry/scoring export if it satisfies the data-source spike.
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
