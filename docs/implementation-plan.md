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
* Optional later spectator camera direction for one venue screen.
* Per-driver telemetry web reports after the UI foundation and local rFactor 2 validation.
* Later PDF/printable telemetry reports.

Current planning status: Phase 0A PoC is complete, the telemetry/source direction is decided, ADR-0001 has accepted .NET / ASP.NET Core for the production host, Phase 0B produced the service/tray/rig-agent/kiosk scaffold, Phase 0C added the packaged runtime skeleton, Phase 1 delivered the live board, and Phase 2 delivered staff controls plus historical boards. The reordered roadmap now moves to Phase 3 UI foundation before local rFactor 2 integration validation, telemetry/report implementation, and venue validation.

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

### Track geometry and TinyPedal reference

* The current repo has no Trackside-specific track geometry generator yet. Existing code exposes raw RF2 world coordinates through the scoring pipeline, but a map outline / track layout is still missing.
* TinyPedal is a useful reference because it separates the source of truth: exact world coordinates from the simulator are one input, while the track layout / geometry is a second, separately constructed model.
* Recommendation: implement the geometry generation in the backend/service layer, not in the raw shared-memory parser. The backend should produce a normalized track geometry payload or SVG-friendly shape once per track, then render live driver positions on the frontend using `posX/posY` values.
* The frontend `/tracker` page should remain a presentation layer that consumes backend-provided track shape data plus live driver positions, rather than attempting to infer the map from lap percent values.

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
* Venue-preview kiosk/admin styling is Phase 3 now that the live data and Phase 2 staff workflows are stable enough to present.
* Local rFactor 2 integration validation, venue validation, operational hardening, and runbook/handover now land in Phases 4, 6, 7, and 9 respectively.
* Captured raw memory-map regression fixtures are useful hardening and should be added when convenient, but they no longer block Phase 1 because live validation has proven the current leaderboard path.

### Phase 2 - Staff controls and historical boards

Status: Phase 2 now has the durable persistence foundation, historical board queries, and the first staff correction workflows in place. A provider-neutral application store interface is in place, with a SQLite adapter registered by the service composition root. The current slice creates the Phase 2 schema for aliases/prepared rig setup, optional driver profiles, sessions, participants, valid lap records, sectors, summary results, monthly track periods, and long-lived derived track-best records; seeds legacy config aliases once; routes current alias reads/writes through the store when persistence is enabled; persists live snapshots from the publisher; records completed laps when the completed-lap count advances; exposes public daily/weekly/monthly/all-time best-lap API responses with track, vehicle/content, session-kind, and date-window filters; adds kiosk UI tabs for monthly, weekly, daily, last session, and live views; adds admin controls to prepare rig/name/profile assignments; adds admin controls to start/reset the active monthly track period; adds an admin Sessions tab for persisted session/participant review plus a per-session `count_for_history` toggle; adds participant display-name correction/exclusion; adds lap correction/invalidation; adds configurable kiosk default display mode; and adds versioned SQLite migrations plus retention cleanup enforcement. Public boards default to one fastest valid timed lap per entered screen name, with an all-laps API mode available for diagnostics or alternate displays. rFactor 2 scoring/driver ids were venue-checked and are treated as stable for a rig within a session.

Current admin/security baseline: the service has cookie-based admin login, local file-backed admin accounts with salted PBKDF2-HMAC-SHA256 password hashes, installer-first initial admin bootstrap, first-run web setup fallback when no admin users exist, protected source configuration, protected admin user management, and protected advanced status. The public kiosk and basic health endpoint remain unauthenticated; detailed paths, source diagnostics, discovery candidates, and admin user status are admin-only. Config and admin-store writes use temp-file replacement; packaged installs restrict the admin-store ACL to SYSTEM and Administrators.

Human prerequisites:

* Confirm staff workflow assumptions: who sets aliases, when aliases reset, and whether historical boards count all sessions by default or require opt-in.

Agent implementation tasks:

* Add SQLite persistence for sessions, participants, laps, sectors, aliases, and summary results. - Implemented behind `ITracksideStore`, including versioned migrations and long-lived derived track-best records used by historical boards.
* Add staff/admin UI for aliases, session inclusion/exclusion, and kiosk display mode. - Implemented for prepared rig/name/profile setup, alias persistence, monthly track set/reset, session history inclusion/exclusion, participant correction/exclusion, lap correction/invalidation, and kiosk default display mode.
* Add correction/exclusion flows for bad sessions or incorrect driver mappings. - Implemented at session, participant, and lap levels. Participant/lap controls are intentionally simple table controls so a later design pass can replace layout without changing APIs.
* Add secondary daily/weekly/monthly best pages. - Started: kiosk UI exposes monthly, weekly, and daily board tabs; monthly defaults to the active monthly track period so a venue can show one track's best times for that month. Daily and weekly advance automatically by local time windows. Monthly track periods never auto-reset; staff decides when to start or reset a monthly period.
* Group historical results by track, car/content, session type, assist preset if available, and date window. - Implemented for track, vehicle/content, session kind, and date windows. Assist-preset filtering remains unavailable because the current scoring source does not expose a reliable assist preset field.

Lap validity rule: best-lap boards must only use full timed laps that rFactor 2 marks valid for timing. Trackside exposes this as `ValidLapFlag`; rFactor 2's underlying values are `0 = do not count lap or time`, `1 = count lap but not time`, and `2 = count lap and time`. Only `2` is eligible for best-time boards. Each per-driver/session lap record stores the lap number, lap time, valid-lap flag, and whether it is valid for lap/timing.

Board ranking rule: public daily/weekly/monthly boards default to `per-driver`, meaning each driver identity appears once with their fastest valid timed lap for the selected track/window. The API also supports `all-laps` for diagnostic/admin displays where repeated laps from the same driver may be useful.

Driver identity rule: a `participant` is one rig/name/vehicle entry in one session. Trackside must not infer global customer identity from a screen name alone. Optional recurring-customer `driver_profiles` can be linked by staff during prepared session setup; casual customers can remain screen-name only. Public display boards deduplicate by entered screen name by default, not by driver profile, so recurring profiles are an add-on for future email/report/telemetry delivery rather than a requirement for normal venue boards.

Prepared session setup rule: staff can prepare rig-to-screen-name assignments before a session and optionally attach a recurring driver profile. That setup carries forward to future sessions until staff changes it or presses clear setup. This matches normal venue operation where the same group may run several back-to-back sessions.

Monthly track rule: staff can start a new active monthly track period. Starting or resetting the monthly track begins fresh displayed stats from that UTC start time without deleting older lap records.

Retention policy direction: detailed lap records are small but operationally short-lived, while track best records and monthly track periods are business history. Current defaults are 35 days for detailed lap records, 730 days for session summaries, indefinite for track best/monthly period records, and 3 days for future high-volume telemetry samples. Cleanup enforcement now runs against long-lived derived track-best records, so raw lap rows can be pruned without removing historical boards.

Validation:

* Staff can assign aliases, mark whether a session counts, review persisted session participants, correct/exclude results, invalidate laps, configure kiosk display mode, and view current plus historical boards using fixture data.
* Persistence survives backend restart. - Covered for aliases and best-lap summaries by store tests that reopen the same database through a fresh store instance.

### Phase 3 - UI foundation and venue-preview styling

Direction: make Trackside look credible before the first venue-facing validation, without changing the stable Phase 2 backend contracts. This phase is a design-system and presentation layer pass over the existing kiosk/admin capabilities. It should not become a marketing site or final branding exercise; the goal is a polished, reusable operational UI foundation that can support telemetry/report pages later.

Human prerequisites:

* Provide any available Gearbox Race Café brand cues, logo files, colors, fonts, venue photos, or screen dimensions. If unavailable, proceed with a neutral motorsport timing-board style.
* Confirm target kiosk screen aspect ratios and expected viewing distance when known. If unavailable, design for common 16:9 desktop/kiosk displays plus responsive browser fallbacks.

Agent implementation tasks:

* Create reusable visual tokens for color, spacing, typography, borders, focus states, table density, alerts, and buttons in the service-hosted static UI and React kiosk app.
* Redesign the kiosk as a presentable race timing display: dark/high-contrast board, clear mode hierarchy, large readable driver names and lap times, strong best-lap/sector states, and intentional empty/error states.
* Redesign the admin shell as a calm operational dashboard: consistent tabs, panels, forms, tables, correction controls, status messages, and safe destructive/invalidating actions.
* Keep UI modular: data fetching, formatting, table rendering, correction controls, and layout/styling should remain separable so future React/admin routing or branding can replace the visual layer without rewriting backend logic.
* Avoid adding new product behavior unless the styling pass reveals a small usability gap that is cheap and local.
* Add fixture screenshots or browser smoke checks where practical so the kiosk/admin layouts do not regress into default tables or unreadable states.

Dependencies:

* Depends on Phase 2 data/API contracts being stable.
* Does not require venue access or live rFactor 2.
* Benefits from screenshots/videos of the actual venue screens, but should not block on them.

Validation:

* Fixture-backed kiosk looks presentable on desktop and common kiosk viewport sizes.
* Admin correction/session/leaderboard controls remain usable after styling.
* UI tests/builds pass, and any screenshot/smoke checks show non-default, readable layouts.
* No backend contract changes are required for later telemetry pages to reuse the layout system.

Exit criteria:

* The kiosk is acceptable to show during local or venue demos without caveats about default browser styling.
* The admin UI is clean enough for staff/operator validation, even if final branding and workflow polish remain later.

### Phase 4 - Local rFactor 2 integration validation

Direction: validate the shared-memory/scoring source against a local rFactor 2 Dedicated Server and client setup before venue access. The shared-memory plugin is widely used in the rFactor 2 ecosystem, and Trackside's source adapter boundaries are designed so field/offset/source quirks can be fixed in the data-source module without rewriting persistence, admin, kiosk, or report logic.

Human prerequisites:

* Provide or run a local Windows environment with rFactor 2 Dedicated Server, one rFactor 2 client, the shared-memory map plugin, and representative content.
* Provide captured logs, screenshots, memory-map snapshots, or remote access if the agent cannot run the local environment directly.
* Confirm plugin version and rFactor 2 build used for local testing.

Agent implementation/support tasks:

* Prepare a local rFactor 2 validation checklist covering server start, client join, session transitions, laps, valid-lap flags, track/vehicle names, driver ids, shared-memory map discovery, service restart, and kiosk reconnect.
* Validate that scoring/driver ids stay stable for a rig within a session, with the current assumption that they do unless local testing proves otherwise.
* Capture a small set of local shared-memory/scoring snapshots for regression fixtures where possible.
* Harden `Trackside.Infrastructure/Rf2` parser/source code only when local validation reveals a concrete mismatch; keep all fixes behind `ILiveSessionSource`, parser, or resolver boundaries.
* Confirm that Phase 2 persistence and correction workflows behave correctly with locally captured real scoring data.

Dependencies:

* Depends on the Phase 1 live source and Phase 2 persistence/correction contracts.
* Requires local rFactor 2 access or captured local data.
* Does not require venue hardware, venue LAN access, or final service host decisions.

Validation:

* Local live board updates from real rFactor 2 scoring data.
* Session changes, track changes, lap validity, and completed-lap persistence behave as expected.
* Service restart and browser reconnect recover without data corruption.
* Any source/parser fixes are covered by fixture or parser tests.

Exit criteria:

* Written local validation notes identify the tested rFactor 2/plugin versions, fields confirmed, any gaps found, and whether the data-source module remains sufficient for venue testing.

### Phase 5 - Telemetry collection and report MVP

Direction: add high-cadence telemetry capture and browser-based report generation before venue validation, so the venue trip can test both live timing and telemetry/report flows together. The current telemetry-report proof-of-concept findings and final source decision live in `docs/telemetry-report-poc-plan.md`. `docs/core-poc.md` records the source-preservation evidence from the completed Phase 0A PoC.

Human prerequisites:

* Confirm the first report scope: channels, comparison baseline, retention expectations, and whether reports are staff-only or customer-visible.
* Provide local or recorded telemetry snapshots if live local rFactor 2 telemetry access is not available.

Agent implementation tasks:

* Implement central server-side telemetry capture as the default mode, using the dedicated-server shared-memory telemetry path first.
* Use a dedicated high-rate telemetry loop, defaulting near `100 Hz`, isolated from lower-rate scoring/session reads so leaderboard stability is not tied to telemetry cadence.
* Add a normalized telemetry ingestion contract in the Application layer so future rig-local collectors can feed the same report pipeline.
* Store short-lived raw telemetry samples with enough session, participant, lap, rig, vehicle, track, and driver-display metadata to associate them with Phase 2 sessions and corrections.
* Add derived report records or summary projections for long-lived report data so raw high-volume telemetry can be pruned safely.
* Build report generation services for per-driver/per-session pages with channels such as throttle, brake, steering, gear, speed, RPM, and lap/sector markers where available.
* Add minimal report APIs: list reports/sessions, fetch one report, fetch channel series, and expose report generation/status.
* Add a basic UI using the Phase 3 layout system: report list, driver/session selector, and simple telemetry graphs. Keep chart components modular so final visual polish can evolve later.
* Suppress or annotate gear/shift analysis when assist settings make that data misleading. If assist preset data is still unavailable, make this explicit in the report payload/UI.

Dependencies:

* Depends on Phase 2 identity/session/lap persistence and Phase 3 UI foundation.
* Benefits from Phase 4 local rFactor 2 validation, but can start from recorded telemetry fixtures.
* Does not require venue access.

Validation:

* Recorded telemetry fixtures render a usable per-driver web report.
* Local rFactor 2 laps, when available, produce telemetry records and a report without breaking live timing.
* Telemetry retention can prune raw samples while keeping generated reports/summaries.
* Telemetry source failure does not crash or block the live board.

Exit criteria:

* A local or fixture-backed user can complete a session, select a driver/session, and view a basic browser telemetry report.

### Phase 6 - Venue validation

Direction: treat venue validation as environment acceptance, not as the first proof that the product works. By this phase, the app should already have presentable UI, local rFactor 2 validation, staff controls, historical boards, and basic telemetry/report flows. The venue trip should identify venue-specific installation, topology, permission, display, and workflow issues.

Human prerequisites:

* Provide physical or remote access during an agreed venue validation window.
* Confirm admin rights and acceptable install locations.
* Provide machine specs for the dedicated server PC, display/kiosk PC, and any candidate service host.
* Confirm existing plugin inventory, especially motion/input plugins on rigs and server-side plugins.

Agent support tasks:

* Prepare an install checklist for the shared-memory bridge and Trackside service.
* Prepare a rollback checklist and canary rollout plan.
* Prepare a venue validation checklist covering scoring fields, telemetry fields, service startup, kiosk connectivity, admin workflows, report generation, restarts, screen readability, and recovery.
* Prepare a venue data-source decision template: dedicated server, spectator client, selected rig, all rigs, or separate host.

Venue validation:

* Confirm whether the shared-memory/scoring/telemetry bridge works on the dedicated server PC and exposes all required live board and telemetry fields.
* If server-side data is insufficient, test fallback sources: spectator client, one selected rig, or all rigs only if needed.
* Confirm a suitable service host: dedicated server PC preferred if headroom/permissions are acceptable; display PC or separate mini PC as fallback.
* Validate kiosk readability and admin usability on actual venue screens and browser hardware.
* Validate telemetry report generation from at least one real or canary venue session.
* Validate coexistence with existing motion/input plugins on one canary rig before broad installation.

Exit criteria:

* Written decisions for venue data source, service host, install path, rollback path, canary rollout order, and any venue-specific source/parser/deployment fixes.

### Phase 7 - Operational hardening and deployment automation

Direction: turn the locally and venue-validated application into something that can survive normal venue operation, restarts, updates, and support handoff. This phase should emphasize reliability, diagnostics, update safety, and rollback over adding new product features.

Human prerequisites:

* Approve target install topology from Phase 6.
* Approve rollout window, update policy, and canary machine/screen.
* Confirm who can administer the service during opening hours.

Agent implementation/support tasks:

* Extend the Phase 0C bundle/install skeleton into a production-ready service/tray install flow for the chosen host.
* Configure final service auto-start/restart settings for the chosen venue service host.
* Package the tray companion/status surface so it auto-starts for the venue user account when appropriate and can reopen dashboards after reboot.
* Add rotating log files for backend services, telemetry capture, update attempts, and any plugin/control surfaces.
* Add dashboard-visible diagnostics for source status, telemetry status, database status, retention cleanup status, and recent errors.
* Add backup/restore guidance or tooling for SQLite data and writable configuration.
* Implement dashboard-visible update checks against a signed/versioned manifest hosted on an operator-controlled server.
* Extend the updater boundary into a staff-approved update flow: download bundle, verify version/signature/checksum, stop affected services, swap files with rollback, restart services, and report success/failure.
* Prevent silent updates during active sessions; require an idle/safe restart window.
* Add manual disable/override switches for every automated feature that can affect venue operations.

Validation:

* Reboot the service host and confirm service recovery, tray/status availability, and dashboards reopening without developer help.
* Test update installation and rollback with a harmless canary build.
* Simulate source outage, telemetry outage, database unavailable/locked cases, and confirm errors are visible without crashing rFactor 2.

Exit criteria:

* A canary install can be installed, restarted, diagnosed, updated, and rolled back with documented commands and visible status.

### Phase 8 - Final polish and workflow refinement

Direction: use feedback from local validation, telemetry work, and venue validation to refine workflows and presentation. This is where the product becomes comfortable for staff and credible for customers, after the technical foundations are stable.

Human prerequisites:

* Provide feedback from staff/operator use, venue screenshots, and any customer-facing display preferences.
* Confirm any final brand/style requirements beyond the Phase 3 venue-preview UI.

Agent implementation tasks:

* Refine kiosk visuals with real venue display constraints: spacing, contrast, animation restraint, empty states, multi-screen display modes, and branding.
* Refine admin workflows based on actual staff use: fewer clicks for common setup, safer correction confirmation, better success/error messaging, and clearer session/report navigation.
* Refine telemetry report UX: graph readability, channel selection, lap comparison, export/download affordances, and customer-safe report sharing if desired.
* Add lightweight automation for repetitive venue tasks, such as preparing default rigs, rotating monthly challenge state, or archiving report links.
* Address edge cases discovered during Phase 6 and Phase 7 that are product-level rather than deployment-level.

Validation:

* Staff can perform normal session prep, correction, board management, and report lookup without developer assistance.
* Kiosk and report pages look intentional on the real target displays.
* No workflow polish breaks existing API contracts or backend invariants.

Exit criteria:

* The application is ready for owner/staff handover documentation and routine operation.

### Phase 9 - Owner runbook and handover

Direction: make the system operable by non-developers. This phase should produce concise owner/staff documentation and any final support artifacts needed for daily operation.

Human prerequisites:

* Confirm who receives the handover and what technical level they have.
* Confirm preferred documentation format and language expectations.

Agent implementation/support tasks:

* Write a short non-technical runbook for venue staff covering daily startup/checks, session prep, aliases, correction/exclusion, kiosk display modes, monthly challenge reset, telemetry reports, and shutdown.
* Document troubleshooting: no live data, no telemetry, wrong driver names, invalid laps, kiosk disconnected, service not running, database/config backup, and failed update.
* Document rFactor 2/Steam update control and re-test procedure.
* Document backup/restore and rollback procedures.
* Add a quick reference checklist for opening, during-session monitoring, closing, and escalation.

Validation:

* A non-developer can follow the runbook to open dashboards, verify status, correct a common mistake, restart services, and know when to escalate.

Exit criteria:

* Owner/staff handover package is complete and matches the deployed venue topology.

### Phase 10 - Optional later enhancements: camera, PDF, printing, incident replay

Direction: keep these features behind the core live timing, staff controls, telemetry, validation, and operational handover path. Implement them only when the user explicitly prioritizes them and the needed hardware/business prerequisites exist.

Human prerequisites:

* For camera work: confirm whether a game-capable spectator PC/license/content is available or must be purchased, and run or support the spectator-only DLC requirement test.
* For printing: choose or install a venue printer and confirm whether staff want automatic printing or manual print/download.
* For incident replay: confirm whether replay is operationally desirable after camera/feed behavior is proven.

Agent implementation tasks:

* Camera: scaffold the C++ camera plugin only after hardware/licensing is viable; read the Studio 397 Internals Plugin SDK; implement one simple mode first, such as rotate all drivers or follow leader; keep plugin logic defensive and narrow.
* PDF/print: add branded PDF generation after the browser report is stable; add manual download/print first; add auto-print only after staff approves the workflow.
* Incident replay: add manual replay trigger before automatic detection; tune impact/off-track/spin rules against local sessions and make every automated behavior easy to disable.

Validation:

* Optional features can run without disturbing live timing, telemetry collection, staff controls, or rFactor 2 clients.
* Print path works on the real venue printer before any automatic printing is enabled.
* Camera/replay features can be deliberately triggered and quickly disabled if disruptive.

---

## 9. Agent-Ready Next Work Package

If an implementation agent starts from this document after Phase 2, begin with Phase 3:

1. Build the reusable UI foundation and venue-preview styling for the service-hosted kiosk/admin pages and the React kiosk app.
2. Keep UI work modular: do not mix data fetching, business rules, and layout styling in ways that would make later telemetry/report pages expensive to add.
3. Use fixture data first and add screenshot/browser smoke checks where practical so the design can be validated locally.
4. After the UI foundation is in place, move to Phase 4 local rFactor 2 integration validation using the modular shared-memory source boundary.
5. Carry forward telemetry source requirements from `docs/telemetry-report-poc-plan.md` for Phase 5; high-cadence shared-memory robustness belongs with telemetry/report work, not the low-cadence live timing board.

Do not restart Phase 0A, Steam setup, venue setup, printer setup, or camera plugin work unless the user explicitly provides those prerequisites and asks for that phase. Venue-specific host/install decisions belong in Phase 6, and optional camera/printing/replay work belongs in Phase 10.

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
