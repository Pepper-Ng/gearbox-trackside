# Phase 0A Core Live-Data PoC

This PoC answers one narrow question: can rFactor 2 session/player scoring data reach a browser with minimal software?

It is intentionally small. It does not implement the final leaderboard architecture, staff controls, persistence, printing, telemetry reports, camera behavior, deployment packaging, or venue hardening.

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

The page currently displays:

* data source and status;
* update counter;
* track;
* session type/code;
* vehicle count;
* current/end session time;
* ambient and track temperatures when available;
* rain value when available;
* driver name;
* vehicle name;
* laps;
* place;
* best lap;
* last lap;
* current lap time;
* sector;
* time behind leader;
* source vehicle ID.

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
Invoke-RestMethod http://127.0.0.1:8877/api/snapshot
```

Expected:

* `/poc` returns HTTP `200`.
* `/api/snapshot` returns `source=mock`, a track name, and one or more drivers.

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
* `Dedicated.exe` did not load the plugin DLL;
* the dedicated-server process has not started a session that causes scoring output to be created;
* the plugin created a map in a Windows namespace/session the PoC process cannot see;
* the monitor or test tool is only checking the normal client map name and not the dedicated-server PID-suffixed names.

To distinguish those cases:

1. Verify the `CustomPluginVariables.json` used by the dedicated server.
2. Confirm the shared-memory plugin entry has `" Enabled"` set to `1`.
3. Confirm `UnsubscribedBuffersMask` does not disable scoring.
4. Confirm `Dedicated.exe` was restarted after changing `" Enabled"`.
5. Check the plugin log output under the rFactor 2 `UserData\Log` area.
6. Run `python services/leaderboard/poc/list_memory_maps.py --pid <PID>` and check whether any rF2 maps are visible/openable.

If plugin log files are created but `RF2SMMP_DebugOutput.txt` remains empty, increase the plugin's debug output settings in `CustomPluginVariables.json` if available for that plugin version. A created-but-empty log file is useful evidence that the DLL loaded, but it is not by itself proof that scoring maps were created.

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
