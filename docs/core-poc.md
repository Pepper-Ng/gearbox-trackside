# Phase 0A Core Live-Data PoC

This PoC answers one narrow question: what useful rFactor 2 scoring and telemetry data can reach a browser with minimal software?

It is intentionally small. It does not implement the final leaderboard architecture, staff controls, durable persistence, printing, telemetry reports, camera behavior, deployment packaging, or venue hardening. It does include a broad diagnostic dashboard so later phases can see which shared-memory fields are available from the chosen host process.

For source-level details on how `rF2SharedMemoryMapPlugin` loads, creates maps, names dedicated-server maps, logs, and publishes scoring data, see `docs/shared-memory-plugin-investigation.md`.

The expected live validation shape is:

```text
[same Windows host that runs rFactor 2 or Dedicated.exe]
  rFactor 2 / Dedicated.exe
  + rF2SharedMemoryMapPlugin
      |
      | Windows named shared-memory map
      v
  Python PoC process
      |
      | local HTTP
      v
  Browser at http://127.0.0.1:8877/poc
```

The browser may run on the same machine or, if the PoC is bound to a LAN interface and firewall rules allow it, another machine. The Python PoC process itself must run on the Windows machine that can see the rFactor 2 shared-memory map.

The page now reads scoring as the required map and telemetry as an optional second map. If telemetry is available, the dashboard joins telemetry rows to scoring rows by vehicle ID and reports whether telemetry appears to cover all scored vehicles, a single vehicle, or only a partial vehicle set.

---

## What Goes On The Host PC

For live testing against a dedicated server, the host PC running `Dedicated.exe` needs two things:

1. The rFactor 2 shared-memory plugin inside the rFactor 2 dedicated-server installation.
2. The PoC Python files somewhere on the same Windows host.

### Required runtime

The PoC uses Python and the Python standard library only.

Required:

* Windows.
* 64-bit Python 3.11+ recommended. The current development machine was validated with Python 3.14.
* No `pip install` step is currently required.
* A browser on the same machine, or network access to the PoC HTTP port if viewing remotely.

Check Python:

```powershell
python --version
```

If multiple Python versions are installed, use the launcher or full path explicitly, for example:

```powershell
py -3 --version
py -3 services/leaderboard/poc/run_poc.py --source mock
```

### Files to copy or clone

Simplest option: clone or copy this whole repository to the host PC.

Minimal files required for the PoC:

```text
services/leaderboard/poc/run_poc.py
services/leaderboard/poc/rf2_poc/__init__.py
services/leaderboard/poc/rf2_poc/rf2_shared_memory.py
services/leaderboard/poc/rf2_poc/server.py
services/leaderboard/poc/rf2_poc/sources.py
services/leaderboard/poc/fixtures/mock_scoring_snapshot.json
```

Optional but recommended on the host:

```text
services/leaderboard/poc/tests/test_sources.py
docs/core-poc.md
```

Keep the folder structure intact. The PoC imports `rf2_poc` relative to `run_poc.py`.

---

## External Components

### rF2SharedMemoryMapPlugin

Python cannot read rFactor 2 scoring data by itself. The PoC does **not** scrape rFactor 2 process memory, network packets, log files, or Steam data.

Live mode requires `TheIronWolfModding/rF2SharedMemoryMapPlugin`. That plugin runs inside rFactor 2 or `Dedicated.exe` and mirrors rFactor 2 internals into Windows named shared-memory maps. The PoC opens the scoring map read-only and decodes the `rF2Scoring` structure.

The relevant live scoring map names are:

* normal rFactor 2 client: `$rFactor2SMMP_Scoring$`;
* dedicated server, non-global map: `$rFactor2SMMP_Scoring$<PID>`;
* dedicated server, global map enabled: `Global\$rFactor2SMMP_Scoring$<PID>`.

The matching telemetry map names are:

* normal rFactor 2 client: `$rFactor2SMMP_Telemetry$`;
* dedicated server, non-global map: `$rFactor2SMMP_Telemetry$<PID>`;
* dedicated server, global map enabled: `Global\$rFactor2SMMP_Telemetry$<PID>`.

`<PID>` is the Windows process ID of `Dedicated.exe`.

The plugin's own README says that, when run in a dedicated server process, it appends the server PID to each shared-memory buffer name. If `DedicatedServerMapGlobally` is enabled, it creates the map in the `Global\` namespace and the Windows account running the dedicated server needs the `Create Global Objects` permission.

### Typical plugin setup

The exact rFactor 2 installation layout may differ, so treat these as guidance to verify against the plugin README and the local dedicated-server folder.

Typical steps:

1. Download or otherwise obtain `rFactor2SharedMemoryMapPlugin64.dll` from `TheIronWolfModding/rF2SharedMemoryMapPlugin` or a known-good existing installation.
2. Place the DLL in the rFactor 2 / dedicated-server plugin folder, typically under a `Bin64\Plugins` path.
3. Start rFactor 2 or `Dedicated.exe` once so rFactor 2 detects the DLL and creates or updates plugin configuration.
4. Stop rFactor 2 / `Dedicated.exe` before editing the plugin configuration.
5. Open the relevant `CustomPluginVariables.json`, usually under a `UserData\<player-or-profile>\` folder.
6. Find the shared-memory plugin entry and set its `" Enabled"` value to `1`. The leading space in `" Enabled"` is intentional in rFactor 2 plugin configuration keys; do not rename it to `"Enabled"` unless the generated file actually uses that spelling.
7. Make sure scoring output is not disabled. The plugin's `UnsubscribedBuffersMask` must not include the `Scoring` flag value `2`.
8. Start rFactor 2 or `Dedicated.exe` again. The plugin should now load and create its shared-memory maps once a session is active.
9. Start a session with AI or human drivers so scoring data changes.

If the PoC reports that the scoring map cannot be opened, the plugin is not loaded, scoring output is disabled, the map name/PID is wrong, or the map is not visible to the Windows user running the PoC.

---

## How Shared Memory Works Here

The live data path is:

1. rFactor 2 calls the shared-memory plugin from inside the game/server process.
2. The plugin copies rFactor 2 scoring state into a Windows named file mapping.
3. The PoC uses Win32 APIs through Python `ctypes`:
   * `OpenFileMappingW` opens an existing named map.
   * `MapViewOfFile` maps it read-only.
   * `ctypes` structures decode the bytes into a Python snapshot.
4. The PoC exposes the normalized snapshot at `/api/snapshot`.
5. The browser page polls `/api/snapshot` and redraws a small table.

The PoC deliberately uses `OpenFileMappingW` instead of Python's `mmap.mmap(...)` live path because Python's named `mmap` can create a new empty map when the requested name does not exist. That would produce a false positive. `OpenFileMappingW` only succeeds when another process, such as the rFactor 2 shared-memory plugin, already created the map.

The upstream plugin/monitor code has historically exposed a small mapped-buffer version wrapper as well as a version header inside the `rF2Scoring` payload. To keep the PoC useful while validating real installations, the reader tries both plausible payload offsets and chooses the decode that looks sane: valid session code, plausible vehicle count, readable text, and plausible vehicle rows. The page shows `Decode offset` so a live run records which layout worked.

---

## What Is Mocked

There are two test/mock levels.

### 1. App fixture mode

Command:

```powershell
python services/leaderboard/poc/run_poc.py --source mock
```

Boundary:

```text
mock JSON fixture -> normalized snapshot -> HTTP API -> browser table
```

This mode does **not** use:

* rFactor 2;
* `Dedicated.exe`;
* `rF2SharedMemoryMapPlugin`;
* Windows shared memory;
* the `SharedMemoryScoringReader` parser.

It is useful for proving that the browser page, HTTP server, JSON shape, refresh loop, and table rendering work. It is intentionally not proof that live rFactor 2 data is available.

The fixture file is:

```text
services/leaderboard/poc/fixtures/mock_scoring_snapshot.json
```

### 2. Shared-memory boundary test

Command:

```powershell
python -m unittest discover services/leaderboard/poc/tests
```

Boundary:

```text
fake Windows named shared-memory map -> production SharedMemoryScoringReader -> normalized snapshot assertions
```

This test creates a temporary Windows named file mapping with the same wrapper-plus-`rF2Scoring` payload shape used by the upstream plugin. It writes fake scoring bytes into that map and then reads them through the production live reader.

This mode does exercise:

* Win32 `OpenFileMappingW` and `MapViewOfFile` usage;
* the plugin map wrapper offset;
* the `rF2Scoring` ctypes layout used by the PoC;
* driver/session field extraction.

This mode does **not** exercise:

* the actual rFactor 2 process;
* the actual `rF2SharedMemoryMapPlugin` DLL;
* plugin installation/configuration;
* dedicated-server PID naming in a real running server;
* Windows cross-user/global namespace permissions;
* whether the venue server exposes all desired fields.

### 3. Live mode

Command examples are below. Live mode is the only mode that proves the actual plugin/server integration.

Boundary:

```text
rFactor 2 / Dedicated.exe -> real rF2SharedMemoryMapPlugin -> Windows named map -> production SharedMemoryScoringReader -> HTTP API -> browser table
```

This is the real Phase 0A go/no-go validation.

---

## Run With Mock Data

From the repository root:

```powershell
python services/leaderboard/poc/run_poc.py --source mock
```

Open:

```text
http://127.0.0.1:8877/poc
```

Expected result:

* The browser shows a bare session summary and driver table.
* The source/status line says `source=mock` and `status=fixture replay`.
* The update counter changes.
* Current-lap values change without a manual refresh.

If port `8877` is already in use:

```powershell
python services/leaderboard/poc/run_poc.py --source mock --port 8890
```

The browser refresh interval is controlled separately from telemetry recording:

```powershell
python services/leaderboard/poc/run_poc.py --source mock --poll-seconds 1 --telemetry-record-hz 50
```

`--poll-seconds` controls how often the browser redraws. `--telemetry-record-hz` controls the PoC background sampler that records telemetry samples for report graphs. Use `--telemetry-record-hz 0` to disable background sampling and record only when the browser or API asks for a snapshot.

The PoC writes its own rotating runtime log files by default under:

```text
services/leaderboard/poc/logs/
```

The console also receives important log lines immediately while the process is running. Routine successful `/api/snapshot` poll responses are intentionally not printed.

---

## Run Against rFactor 2 Shared Memory

### Normal rFactor 2 client

If the shared-memory plugin is loaded in a normal rFactor 2 client, the scoring map should usually be `$rFactor2SMMP_Scoring$`:

```powershell
python services/leaderboard/poc/run_poc.py --source shared-memory
```

Open:

```text
http://127.0.0.1:8877/poc
```

### Dedicated server

For a dedicated server, the process ID must be the Windows PID of the running `Dedicated.exe` process, not the PID of `rFactor2.exe`, Steam, the launcher, the monitor app, or Python.

Find it with one of these commands. The second command is more tolerant of executable names such as `rFactor2 Dedicated.exe`:

```powershell
Get-Process Dedicated | Select-Object Id, ProcessName, Path
Get-CimInstance Win32_Process | Where-Object { $_.Name -like '*Dedicated*.exe' } | Select-Object ProcessId, Name, ExecutablePath, CommandLine
```

Then pass that process ID value to the PoC:

```powershell
python services/leaderboard/poc/run_poc.py --source shared-memory --pid <PID>
```

No repository config file is required for this value. It is a runtime argument because the PID changes each time `Dedicated.exe` restarts.

The PoC will try these scoring map names in order:

```text
$rFactor2SMMP_Scoring$<PID>
Global\$rFactor2SMMP_Scoring$<PID>
$rFactor2SMMP_Scoring$
```

This is different from normal client mode. The upstream shared-memory plugin appends the `Dedicated.exe` PID when it runs inside a dedicated server so that multiple dedicated-server instances can run at the same time without map-name collisions.

If rFactor 2 and `Dedicated.exe` are installed in the same folder, they may share content, installed packages, `Bin64\Plugins`, and some `UserData` structure, but they are still separate processes. If both executables are running and both load the shared-memory plugin, they can expose different maps at the same time:

* `rFactor2.exe` exposes the normal client map name such as `$rFactor2SMMP_Scoring$`.
* `Dedicated.exe` exposes a PID-suffixed map name such as `$rFactor2SMMP_Scoring$12345` or `Global\$rFactor2SMMP_Scoring$12345`.

A monitor application that only opens the normal client map name may show data when a client joins but show nothing for the dedicated server. That does not necessarily prove that the dedicated-server plugin failed; it may only mean the monitor is not looking for the PID-suffixed dedicated-server map. For the PoC, use `--pid <PID>` or `--map-name` to target the dedicated-server map explicitly.

If needed, pass the exact map name:

```powershell
python services/leaderboard/poc/run_poc.py --source shared-memory --map-name 'Global\$rFactor2SMMP_Scoring$12345'
```

If scoring and telemetry need different exact names, pass both:

```powershell
python services/leaderboard/poc/run_poc.py --source shared-memory --map-name 'Global\$rFactor2SMMP_Scoring$12345' --telemetry-map-name 'Global\$rFactor2SMMP_Telemetry$12345'
```

### Auto fallback mode

`auto` tries shared memory first and falls back to fixture replay if the map cannot be opened:

```powershell
python services/leaderboard/poc/run_poc.py --source auto --pid <PID>
```

Use this only for convenience. If the browser says `shared memory unavailable; using fixture replay`, it is **not** a live proof.

---

## Inspect Visible Memory Maps

Use this diagnostic when the plugin appears to load but `run_poc.py --source shared-memory` cannot open any map.

Without a dedicated-server PID:

```powershell
python services/leaderboard/poc/list_memory_maps.py
```

With a dedicated-server PID:

```powershell
python services/leaderboard/poc/list_memory_maps.py --pid <PID>
```

The command does two things:

1. It actively probes known rF2 shared-memory map names with `OpenFileMappingW`, including PID-suffixed dedicated-server variants when `--pid` is supplied.
2. It enumerates named Windows `Section` objects visible to the current user/session and filters for `rFactor2SMMP` by default.

Example useful output:

```text
rF2 map open probes:
  OPEN    $rFactor2SMMP_Scoring$
  missing $rFactor2SMMP_Scoring$18228 (Windows error 2)

Visible named Section objects:
  \BaseNamedObjects\$rFactor2SMMP_Scoring$ [Section]
```

Use `--all` to print all visible named Section objects, not just rF2 matches:

```powershell
python services/leaderboard/poc/list_memory_maps.py --all
```

Limitations:

* This lists named Section objects visible to the Windows user/session running the diagnostic.
* It cannot list private unnamed mappings.
* It may not show objects in other sessions or namespaces if the current user cannot access them.
* If the diagnostic finds no rF2 maps, but plugin log files appear, the DLL may be loaded but not creating output maps for that process/session/configuration.

---

## Viewing From Another Machine

By default, the PoC binds to `127.0.0.1`, so only the host PC can open it.

To expose it on the LAN:

```powershell
python services/leaderboard/poc/run_poc.py --source shared-memory --pid <PID> --host 0.0.0.0 --port 8877
```

Then open:

```text
http://<host-pc-ip>:8877/poc
```

This may require a Windows Firewall rule for the selected port. Do not expose this PoC to the public internet; it has no authentication or hardening.

---

## Current PoC Output

The page currently displays a diagnostic dashboard rather than a polished leaderboard. It shows:

* data source and status;
* update counter;
* scoring and telemetry map names;
* scoring and telemetry decode offsets;
* telemetry status and scope: unavailable, single vehicle, partial vehicle set, or all scoring vehicles;
* track;
* session type/code;
* game phase and realtime state;
* vehicle count;
* current/end session time;
* ambient and track temperatures when available;
* rain, cloud, path wetness, and wind when available;
* a field coverage matrix for the project-critical data points;
* fastest lap and fastest sector summaries from currently visible scoring data;
* driver name;
* vehicle name;
* laps;
* place;
* best lap;
* best-sector and best-lap sector split data where the scoring map exposes it;
* last lap;
* last-lap sector split data where available;
* current lap time;
* current sector;
* lap distance and percentage of track completed;
* world coordinates;
* local velocity, acceleration, and speed where available;
* time behind leader;
* finish status and race order fields;
* joined telemetry per driver when available: throttle, brake, steering, gear, G-force, speed, engine RPM, tire compounds, fuel, impact values, and related status fields;
* telemetry recording status and sample count;
* flag summary: green, local yellow, full-course yellow/safety car, race halt/stopped, and sector yellow values when scoring exposes them;
* current in-memory recorded session history;
* completed in-memory session history at `/history` and `/api/history`;
* finalized-session telemetry viewer links at `/telemetry?session=<session-id>` and JSON at `/api/reports/<session-id>`;
* a telemetry viewer at `/telemetry` that can list stored recordings, load old `report.json` files after a collector restart, or open local `report.json` / `telemetry_samples.jsonl` files;
* full-resolution report graphs from the recorded telemetry samples, preserving the collector's captured sample stream instead of resampling to a smaller graph axis;
* lap classification for telemetry reports: proper laps, partial laps, outlaps, inlaps, and formation/non-timed laps. Only proper laps are used for fastest/reference report laps, while the viewer still allows selecting other laps for diagnosis.

The in-memory history is deliberately temporary. It is built by observing snapshots while the PoC process is running. It is useful for proving whether lap/sector values can be captured at the time they appear, but it is not durable storage and is not the final historical leaderboard implementation.

Important shared-memory interpretation notes:

* The scoring map is still the required live source.
* The telemetry map is optional. If it is missing, the dashboard still shows scoring and reports telemetry as unavailable.
* The plugin requests all-vehicle telemetry when telemetry is subscribed. The dashboard reports the observed telemetry scope by comparing telemetry rows with scored vehicle rows.
* The browser can redraw once per second while the recorder samples faster in the background. This means visible dashboard update cadence is not the telemetry-map cadence.
* G-force values come from local vehicle acceleration. The PoC records lateral (`x`), vertical (`y`), longitudinal (`z`), and magnitude values.
* Report generation starts on a background thread when a session finalizes. If the report page is opened while generation is still running, it displays a building state and polls the report API until data is ready.
* Some rFactor 2 sector fields are cumulative to sector 2. The PoC derives split values where there is enough information and leaves unavailable values blank.
* Full per-lap history is not directly dumped as a complete archive by the scoring snapshot. The PoC records lap/sector values as they are observed so we can prove whether a future service can compile history live.
* Report sample files are written under `services/leaderboard/poc/telemetry-recordings/`, which is gitignored. The `/api/recordings` endpoint lists stored recording folders visible to the current PoC process.

### Telemetry Cadence Finding From Real Captures

Two real captured sessions are kept as regression fixtures under `services/leaderboard/poc/tests/data/`:

* `bahrain-gp-2014-practice-ac00312535`;
* `bahrain-gp-2014-qualifying-e07b77aca6`.

These captures support the user's observation that the current server-side PoC telemetry is not consistently near 50 Hz per driver:

* Practice capture: 48,181 stored telemetry samples, 24 proper laps, 10 excluded laps. Proper-lap effective sample rate ranged from about 1.4 Hz to 14.2 Hz, with an 8.2 Hz median.
* Qualifying capture: 54,269 stored telemetry samples, 7 proper laps, 6 excluded laps. Proper-lap effective sample rate ranged from about 26.5 Hz to 30.0 Hz, with a 28.0 Hz median.
* In both captures, `lap_percent` repeats for most adjacent samples. This is expected because lap distance / track percent currently comes from scoring, while throttle, brake, steering, gear, speed, and G-force come from telemetry.
* Speed changes in almost every adjacent sample, steering changes often, but throttle and brake change less frequently. This suggests that some visual coarseness is real captured data cadence or channel quantization, not only graphing.
* `gear = 0` appears frequently in the raw data, including proper laps. Some of this is expected for cars stopped or in neutral, but moving single-sample neutral values can look like unrealistic spikes. The viewer smooths short moving-neutral blips for display only; raw samples remain unchanged.

This evidence does **not** yet prove that dedicated-server shared memory cannot provide adequate telemetry. It proves that the current Python PoC collector, running against the server-side maps and combining scoring plus telemetry reads, did not capture stable 50 Hz per-driver telemetry in these sessions.

Recommended next implementation step: keep the central server-side collector as the preferred architecture for the next PoC iteration, but optimize and measure it before introducing six rig-local collectors. For a maximum of six drivers, the server-side all-car telemetry volume should be manageable and it avoids installing and synchronizing services on every simulator. The next collector should:

* decouple high-rate telemetry reading from lower-rate scoring/session reading;
* sample the telemetry map on its own target loop at 50 Hz or faster;
* read scoring/lap/session state at a lower rate and join cached scoring data to telemetry samples;
* batch or queue writes so file I/O and JSON serialization do not block the telemetry read loop;
* log actual loop timing, dropped/late samples, and per-driver effective sample rates;
* prefer a time-based report axis for telemetry channels, while using track percent only as a positional aid until a higher-resolution distance source is proven.

If an optimized central collector still cannot sustain roughly 45-50 Hz per active driver on the venue server, then run a bounded fallback spike with one rig-local collector. A rig-local collector may provide better own-car telemetry fidelity, but it adds deployment and operational cost: shared-memory plugin installation on every setup, a service on every setup, six more points of failure, and post-session synchronization back to the central service. The final architecture should only move telemetry capture to each rig if the optimized server-side collector cannot meet the report-quality target.

For the report/printing proof-of-concept tracker, see `docs/telemetry-report-poc-plan.md`.

The goal is not to make this page pretty. The goal is to reveal what data is present and whether it updates.

---

## Validation Commands

Run from the repository root:

```powershell
python -m compileall -q services/leaderboard/poc
python -m unittest discover services/leaderboard/poc/tests
```

Mock smoke test:

```powershell
python services/leaderboard/poc/run_poc.py --source mock
```

In another terminal:

```powershell
curl.exe --noproxy "*" -s -o NUL -w "%{http_code}" http://127.0.0.1:8877/poc
curl.exe --noproxy "*" -s -o NUL -w "%{http_code}" http://127.0.0.1:8877/history
Invoke-RestMethod http://127.0.0.1:8877/api/snapshot
Invoke-RestMethod http://127.0.0.1:8877/api/history
```

Expected:

* `/poc` returns HTTP `200`.
* `/history` returns HTTP `200`.
* `/api/snapshot` returns `source=mock`, a track name, one or more drivers, `field_coverage`, `highlights`, and mock telemetry values.
* `/api/history` returns the current in-memory session record and any completed sessions observed since the PoC process started.
* Once a live or synthetic session finalizes, `/history` shows a telemetry report link for that completed session.

---

## Troubleshooting

### `OpenFileMappingW(...) failed: Windows error 2`

The named map does not exist from the PoC process's point of view.

Check:

* Is rFactor 2 or `Dedicated.exe` running?
* Is `rF2SharedMemoryMapPlugin` installed and loaded?
* Has rFactor 2 generated the plugin entry in `CustomPluginVariables.json`, and is `" Enabled"` set to `1` for that plugin?
* Is scoring output enabled? `UnsubscribedBuffersMask` must not include `2`.
* For dedicated-server mode, did you pass the current `Dedicated.exe` PID with `--pid <PID>`?
* Did `Dedicated.exe` restart after you collected the PID? If yes, collect the new PID.
* Are you running the PoC under a Windows user/session that can see the map?
* If using `Global\...`, does the dedicated-server account have `Create Global Objects` permission?
* If a monitor app shows client data but not server data, confirm whether the monitor supports PID-suffixed dedicated-server map names. Some tools may only look for `$rFactor2SMMP_Scoring$`.

For a hard dedicated-server check, prefer:

```powershell
Get-CimInstance Win32_Process | Where-Object { $_.Name -like '*Dedicated*.exe' } | Select-Object ProcessId, Name, ExecutablePath, CommandLine
python services/leaderboard/poc/run_poc.py --source shared-memory --pid <PID>
```

If this still fails, try the exact map names shown in the dedicated-server section with `--map-name`.

If all dedicated-server map names fail but client mode works, the most likely explanations are:

* the plugin is enabled for the client profile but not for the profile/configuration used by `Dedicated.exe`;
* `Dedicated.exe` loaded the plugin and receives session callbacks, but mapped-buffer startup did not complete;
* the plugin created a map in a Windows namespace/session the PoC process cannot see;
* the monitor or test tool is only checking the normal client map name and not the dedicated-server PID-suffixed names.

To distinguish those cases:

1. Verify the `CustomPluginVariables.json` used by the dedicated server.
2. Confirm the shared-memory plugin entry has `" Enabled"` set to `1`.
3. Confirm `UnsubscribedBuffersMask` does not disable scoring.
4. Confirm `Dedicated.exe` was restarted after changing `" Enabled"`.
5. Check the plugin log output under the rFactor 2 `UserData\Log` area.
6. Run `python services/leaderboard/poc/list_memory_maps.py --pid <PID>` and check whether any rF2 maps are visible/openable.

If the internals telemetry/scoring output files update while only the server is running, the DLL is loaded and receiving callbacks. That still does not prove that mapped files were created; use `list_memory_maps.py --pid <PID>` to verify maps directly.

If `RF2SMMP_DebugOutput.txt` remains empty with `DebugOutputLevel = 1`, that is expected unless an error is logged. Startup/configuration lines use `CriticalInfo`. `DebugOutputLevel = 15` is enough for startup/config/error/warning output; `255` adds deeper timing/synchronization/verbose tracing.

```json
"DebugOutputLevel":15,
"DebugOutputSource":32767,
"DebugISIInternals":1
```

Then restart `Dedicated.exe` with no client connected and look for `Files mapped successfully.` or a mapping failure. If debug output appears only after a client joins, it is likely coming from the client plugin instance rather than the dedicated-server instance.

If global map creation fails, either grant the server account the Windows `Create Global Objects` user right, or set `DedicatedServerMapGlobally = 0`, restart `Dedicated.exe`, and run the PoC from the same Windows user/session as the server.

If the debug log still reports `DedicatedServerMapGlobally: 1` after editing it to `0`, the server is reading a different `CustomPluginVariables.json` than the one being edited, or the server was not restarted after the change.

If `list_memory_maps.py --pid <PID>` shows only non-PID maps while a client is connected, those are client maps. Dedicated-server maps should be PID-suffixed or global PID-suffixed.

### Browser shows impossible values in live mode

Examples: `128` vehicles when only a few cars are present, `Unknown (<large number>)` session, very large sector values, negative/changing lap counts, or many blank rows.

These usually mean the reader opened a real map but decoded the bytes with the wrong layout or offset. The PoC now auto-detects the more plausible scoring payload offset and filters unlikely empty vehicle rows. If this still happens, record:

* source line from the page, including `Decode offset`;
* map name used;
* whether the map came from client or dedicated server;
* shared-memory plugin version;
* a screenshot or `/api/snapshot` JSON sample.

Then treat the run as partial evidence only: the map exists, but the struct layout needs adjustment before the data can be trusted.

### Browser opens but shows fixture data

Check the source/status line. If it says `source=mock` or `shared memory unavailable; using fixture replay`, you are not proving live rFactor 2 integration yet.

Use `--source shared-memory` for a hard failure when the live map cannot be opened.

### Browser cannot connect

Check:

* The PoC terminal is still running.
* The URL uses the selected port.
* Another process is not using the same port.
* If viewing from another PC, use `--host 0.0.0.0` and allow the port through Windows Firewall.

### Table is empty in live mode

Check:

* Is a session loaded?
* Are AI or human drivers actually connected?
* Is the session paused or still at a menu?
* Does the shared-memory plugin monitor tool show scoring vehicles?

---

## Go/No-Go Criteria

Proceed toward the full live leaderboard if live mode can show changing driver/session data from either:

* `$rFactor2SMMP_Scoring$` on a normal rFactor 2 client; or
* `$rFactor2SMMP_Scoring$<PID>` / `Global\$rFactor2SMMP_Scoring$<PID>` on a dedicated server.

If the dedicated server does not expose the needed fields, next test a spectator client or one sim rig as the data source.

Record the live result in this document or a follow-up spike note:

* host type: client, dedicated server, spectator client, or rig;
* exact command used;
* memory map name that worked;
* fields visible;
* fields missing;
* whether the browser updated when AI/session data changed.
