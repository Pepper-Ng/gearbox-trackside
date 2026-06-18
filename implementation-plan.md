# Gearbox Race Café — rFactor 2 Trackside — Implementation & Test Plan

Trackside is a toolset that provides:
* Spectator camera direction
* Automatic leaderboards
* Printable per-driver telemetry reports

Tailored to Gearbox Race Café's rFactor 2 simulator setup.

Scope: add (1) spectator screens with auto/fixed camera + incident replay, (2) an automatic leaderboard, (3) per-driver telemetry PDF reports with auto-print, to an existing working rFactor 2 setup (6 sim rigs + 1 orchestrating server, Steam, F1 car/track content).

Tags used below: **[DEV]** = pure coding, doable by an agent with no Steam/hardware access. **[STEAM]** = needs a real rFactor 2 client + server running (human must drive this part). **[VENUE]** = needs the friend's actual machines/network/printer.

---

## 1. Feasibility Summary

| Feature | Feasibility | New native plugin required? | Needs venue access to start? |
|---|---|---|---|
| Automatic leaderboard | High — proven pattern | No | No |
| Telemetry stats → PDF → print | High — proven pattern | No | No |
| Auto/fixed spectator camera | High — official API exists, community precedent exists | Yes (small one) | Only for final calibration |
| Incident detection → auto replay | Medium-high — mechanism is documented, exact trigger needs tuning | Same plugin as above | Only for final calibration |

Overall: no exotic R&D needed. The two "data" features need zero new game plugin — they're ordinary software reading data the game already exposes. Only the camera feature needs a small custom plugin, and even that is buildable and testable on a generic local install before ever touching the venue.

---

## 2. Architecture

Two independent tracks, both fed by the same plugin layer running inside rFactor 2 clients:

```
[Existing setup: 5 sim rigs + orchestrating server]
            │
            ├──> rF2 shared-memory plugin (telemetry + scoring bridge)
            │         ├──> Leaderboard service ──> Display board
            │         └──> Report/PDF service ──> Venue printer
            │
            └──> Camera director plugin (in-process, camera control)
                      └──> Spectator screen(s)
```

- **Telemetry/scoring bridge**: the existing open-source `rF2SharedMemoryMapPlugin` mirrors live telemetry (throttle/brake/steering/gear/impacts, per car) and scoring (positions/lap times/sectors) into shared memory. Any ordinary program can read it — no compiled game plugin needed for the leaderboard or PDF features.
- **Camera director plugin**: a small custom DLL built against the official, free Studio 397 "Internals Plugin SDK". It uses the documented `WantsToViewVehicle()` callback, which lets a plugin pick the camera/car shown or drive instant-replay controls — this is the intended, documented mechanism for exactly this use case (a private community tool, GRTvPlugin, already does something similar, confirming the approach works).

---

## 3. Key Risks / Open Questions

| # | Risk / question | Why it matters | How to resolve | Blocking? |
|---|---|---|---|---|
| R1 | Does a spectator-only client need to **own** the F1 car/track DLC to join? | Determines cost of N spectator screens (N extra Steam licenses + content, or not) | 30-min test: join server as spectator from an account that doesn't own the DLC | Only blocks **hardware/license purchasing**, not development |
| R2 | Independent camera per screen vs. one feed split to many TVs | Changes hardware cost drastically (N PCs+licenses vs. 1 PC + HDMI splitter) | Business decision with the venue owner | Blocks **venue rollout phase only** |
| R3 | Exact telemetry/rules field used for "off track" / "impact" detection | Needed for reliable incident-triggered replays | Read the public SDK headers directly (one afternoon) | No — resolvable any time, doesn't block other work |
| R4 | Is the orchestrating server the free standalone Dedicated Server binary, or a full client acting as host? | Slightly changes where the scoring-side plugin should live | Ask the friend / inspect his setup | No — doesn't block development |
| R5 | rFactor 2 client auto-updates via Steam can change internal struct layouts | Could silently break the plugin after a Steam update | Pin/manually control updates on venue PCs; re-test plugin after any update | Ongoing operational risk, not a start blocker |

None of these block starting development today.

---

## 4. Local Dev/Test Environment (no venue access needed)

This replicates "1 client + 1 server" locally so most of the work can be built and validated before ever touching the friend's machines.

1. **[STEAM]** Buy rFactor 2 (base game) on Steam — this is your one licensed client.
2. **[DEV]** Install the **free, separate** Dedicated Server (Steam App ID `400300`) using an **anonymous** SteamCMD login, in its own folder — this avoids the known "Steam thinks I'm already playing" conflict that happens if the server is tied to your personal account:
   ```
   steamcmd +login anonymous +force_install_dir C:\rf2-dedicated +app_update 400300 +quit
   ```
3. **[DEV]** Note: the dedicated server has **no official Linux build**. If you want a VM for isolation, use a Windows VM. Running it in a plain folder on your existing Windows PC (no VM) is simpler and equally valid.
4. **[STEAM]** Start the dedicated server, point your client at `localhost`, fill the rest of the grid with AI drivers (only one human seat exists with one license).
5. **[STEAM]** Confirm: AI race runs, your client generates telemetry, scoring updates — this is your live validation rig for everything below.
6. Note: you do **not** need the specific F1 DLC content for early development — any bundled/cheap content works for building and testing the generic logic. Buy/borrow the F1 packs only when calibrating the final camera-name/track-specific details.

---

## 5. Phased Implementation & Test Plan

Ordered so the earliest phases need the least setup and produce visible value fastest.

### Phase 0 — Project bootstrap **[DEV]**
- [ ] Create repo with folders: `/plugin` (C++ camera director), `/services` (leaderboard, report generator), `/web` (kiosk display), `/docs`.
- [ ] Download the official Studio 397 Internals Plugin SDK headers (free, public).
- [ ] Vendor/reference the open-source `rF2SharedMemoryMapPlugin` source as the telemetry/scoring bridge — do not reimplement this.
- [ ] Set up build tooling: MSVC for the C++ plugin; Python (or chosen service stack) for the backend services.
- **Exit test**: solution builds an empty plugin DLL and an empty service stub. No Steam needed.

### Phase 1 — Leaderboard MVP (data track) **[DEV]**
- [ ] Write a shared-memory reader against the published scoring struct layout.
- [ ] Build ingestion service: tag sessions "counts for leaderboard" (simple flag/toggle), capture entrant names + lap/race times + finishing order on session end, write to SQLite.
- [ ] Build minimal kiosk webpage showing current standings/winners.
- **Exit test (mocked) [DEV]**: feed the service synthetic/recorded scoring snapshots, verify correct ranking, name attribution, and correct inclusion/exclusion based on the flag.
- **Exit test (live) [STEAM]**: run against the local test rig (Section 4), confirm real AI-race results populate the board correctly.

### Phase 2 — Telemetry → PDF report MVP (data track) **[DEV]**
- [ ] Build telemetry capture: per-driver, per-lap channel logging (throttle/brake/steering/gear) from the shared memory.
- [ ] Build a personal-best store (per driver/track/car) and a "vs. personal best" comparison aligned by distance, not time.
- [ ] Build chart + PDF generation; suppress the gear chart when the session's assist config marks auto-shift as active (read this from the existing central assist config rather than inferring it).
- [ ] Build a print trigger (manual button first; "auto on session end" later) sending to a configured network printer.
- **Exit test (mocked) [DEV]**: feed synthetic telemetry CSVs, verify charts render correctly and gear-suppression logic works, with no Steam involved at all.
- **Exit test (live) [STEAM]**: drive real laps on the test rig with assists on/off, confirm PDF content and printer trigger.

### Phase 3 — Camera director plugin v1 **[DEV]**
- [ ] Scaffold a C++ plugin implementing `WantsToViewVehicle()`.
- [ ] Implement basic auto-cycle modes: round-robin through cars, "follow leader," trackside vs. onboard alternation.
- [ ] Implement a manual override: a simple local control file (JSON/INI) the venue owner (or a tiny control UI) edits to pin a fixed camera/car — favor this over building a network server inside the plugin, to keep the in-process code minimal and stable.
- **Exit test [STEAM]**: load the plugin in the test rig's client while spectating an AI-only race; verify cycling behaves sensibly and the override file correctly pins a fixed view.

### Phase 4 — Incident detection & auto-replay **[DEV]**
- [ ] Confirm exact telemetry/rules fields for impact magnitude / off-track condition by reading the SDK headers (resolves Risk R3).
- [ ] Implement a detection heuristic (impact magnitude / sudden deceleration thresholds).
- [ ] Wire detection into the plugin's replay-trigger path (the same callback supports commanding instant-replay).
- **Exit test [STEAM]**: deliberately cause AI/your-own-car spins and off-track excursions on the test rig; verify replay triggers fire on real incidents and don't false-positive on minor track-edge touches; tune thresholds.

### Phase 5 — Venue-scale architecture decision **[STEAM + business]**
- [ ] Run the Risk R1 test against the friend's actual server: join as spectator from an account that doesn't own the F1 DLC, see if it's allowed.
- [ ] Decide, with the venue owner: independently-directed screens (N spectator PCs + N licenses) vs. one feed split via HDMI splitter to multiple identical screens (cheap, all screens show the same thing).
- [ ] Procure hardware/licenses accordingly.
- **Exit criteria**: a costed, agreed hardware/licensing plan.

### Phase 6 — Venue rollout **[VENUE]**
- [ ] Load the shared-memory plugin on the 6 sim rigs and/or server (wherever scoring/telemetry is authoritative).
- [ ] Deploy the camera director plugin to the spectator PC(s); on-site calibration of camera names for the actual F1 track packs in use (this varies per track and can only be done with the real content).
- [ ] Wire up the leaderboard display board and the venue printer on the real network.
- [ ] Give the venue owner the override controls: pin camera, mark session "counts for leaderboard," trigger/auto-enable printing.
- **Exit test [VENUE]**: run a real multi-driver race end-to-end; verify spectator screen, leaderboard, and printed reports all work together.

### Phase 7 — Hardening, training, handover **[VENUE + DEV]**
- [ ] Add manual disable/override switches for every automated feature.
- [ ] Add logging (rotating log files) on all services and the plugin's external control surface.
- [ ] Configure backend services as auto-restarting Windows services.
- [ ] Write a short non-technical runbook for the venue owner (separate from developer docs).
- [ ] Freeze/pin the rFactor 2 client version on venue PCs; document the re-test procedure required before any future Steam update is applied.

---

## 6. Project Setup & Engineering Recommendations

**Repo structure**: single monorepo — `/plugin`, `/services`, `/web`, `/docs` — given the small team size and tight coupling between components; split later only if it grows past one team.

**Stack**:
- Plugin: **C++**, the only option the Internals Plugin SDK supports. Keep its scope to camera/replay control only — push everything else (parsing, storage, charts, printing) into external services. The plugin runs inside the game process, so a crash there can take down a race; minimize and defensively code this surface.
- Backend services (leaderboard, report generator): **Python** is recommended for speed of iteration and its mature charting/PDF ecosystem (pandas, matplotlib, reportlab/weasyprint); package and run as Windows services (e.g. via NSSM) for auto-restart and unattended operation. A .NET/C# stack is a reasonable alternative if the team is more comfortable there.
- Storage: **SQLite** to start — zero-ops, file-based, plenty for one venue's volume. Migrate to Postgres only if multi-venue or cloud sync is ever needed.
- Display/kiosk: a small local web app shown via a browser in kiosk mode — simplest path, and leaves room for remote monitoring later.

**SDK & dependency policy**:
- Use the **official** Studio 397 Internals Plugin SDK directly for the camera plugin — no alternative exists.
- **Reuse, don't reimplement**, the open-source `rF2SharedMemoryMapPlugin` for telemetry/scoring export — it's mature, widely used by other third-party tools, and lower risk than a custom equivalent. Multiple plugins can be loaded simultaneously, so this coexists fine with the custom camera plugin.
- Keep the custom plugin's responsibilities narrow (camera/replay only) to limit blast radius of any bug.

**Deployment & update strategy**:
- Maintain the local test rig (Section 4) permanently as a **staging environment** — every change gets validated there before touching the venue.
- Roll out venue changes as a **canary**: one sim rig or one spectator screen first, verify, then the rest.
- Package each component as a versioned, file-based release (DLL + config; service zip/installer) so rollback is just restoring the previous file — no complex pipeline needed at this scale.
- Do **not** let the venue PCs auto-update rFactor 2 silently; control updates manually and re-validate plugin compatibility against the staging rig before applying them venue-wide (Risk R5).

**Stability practices**:
- Default to a safe fixed camera if the director's logic errors out, rather than failing loudly mid-race.
- Run all backend services as auto-restarting Windows services so a crash in the leaderboard/report pipeline never affects an in-progress race.
- Add rotating log files everywhere — venue operation won't have a developer watching a console live.
- Always provide a manual override/disable for every automated feature (camera auto-mode, auto-print) so the venue owner is never stuck waiting on a developer.

**Process/maintenance**:
- Git with CI: a Windows build job for the plugin DLL, lint/test job for the services.
- Lightweight issue tracking (e.g. GitHub Issues) — appropriate for this scope.
- Two tiers of docs: a developer doc (this plan + architecture) and a short non-technical runbook for the venue owner's day-to-day operation.

---

## 7. Reference Materials

- Studio 397 Internals Plugin SDK — official, free, distributed via Studio 397's modding resources/forum.
- `rF2SharedMemoryMapPlugin` (TheIronWolfModding) — open-source telemetry/scoring shared-memory bridge, widely used by third-party rF2 tools.
- SteamCMD — used for the anonymous, account-free install of the Dedicated Server (App ID `400300`).
- rFactor 2 (client, base game) — Steam App ID `365960`.
