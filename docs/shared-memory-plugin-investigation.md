# rF2 Shared-Memory Plugin Investigation

This note summarizes the source-level behavior of `TheIronWolfModding/rF2SharedMemoryMapPlugin` as vendored under `vendor/rf2-shared-memory-map/src`.

The practical goal is to document how the plugin loads, creates maps, names dedicated-server maps, logs status, and publishes scoring data so future development can use the shared-memory data path confidently.

---

## High-Level Conclusion

The plugin source says that shared-memory maps are created during `SharedMemoryPlugin::Startup()`, not lazily after the first vehicle joins. If startup completes successfully, the maps should exist even before useful scoring data is written.

Therefore:

* The shared-memory map approach is valid for both client and dedicated-server processes when the plugin is enabled and the Windows namespace is correct.
* Client maps use base names such as `$rFactor2SMMP_Scoring$`.
* Dedicated-server maps use PID-suffixed names such as `$rFactor2SMMP_Scoring$<PID>`.
* The PoC should target the dedicated-server PID when using `Dedicated.exe` as the source of truth.
* No source patch to the DLL is currently justified. The public plugin behavior matches the source and works once configuration and namespace settings are correct.

Post-investigation addendum (June 2026):

* Phase 0A confirmed the dedicated-server scoring and telemetry maps are viable for the current project scope.
* Telemetry collector policy and source-modularity decisions are documented in `docs/telemetry-report-poc-plan.md`.

---

## Plugin Lifecycle

The DLL exports the standard rFactor 2 internals plugin entry points:

* `GetPluginName()`
* `GetPluginType()` returning `PO_INTERNALS`
* `GetPluginVersion()` returning `7`
* `CreatePluginObject()`
* `DestroyPluginObject()`

rFactor 2 creates a `SharedMemoryPlugin` instance and calls its internals callbacks.

Important callbacks:

* `Startup(long version)` creates all memory maps.
* `Shutdown()` clears and releases them.
* `StartSession()` / `EndSession()` update extended state and optionally write internal trace files.
* `EnterRealtime()` / `ExitRealtime()` update realtime state.
* `UpdateScoring(...)` writes scoring data about five times per second once rFactor 2 sends scoring updates.
* `UpdateTelemetry(...)` writes telemetry data.

---

## Configuration

The plugin uses rFactor 2 `CustomPluginVariables.json` via `GetCustomVariable(...)` and `AccessCustomVariable(...)`.

### Configuration integrity comes first

A field incident confirmed that malformed `CustomPluginVariables.JSON`, left behind by a PC crash, can make rFactor 2 Dedicated exit during startup without a useful visible error. Removing the malformed file caused rFactor 2 to regenerate it and restored normal startup.

Before debugging an individual plugin:

1. preserve the active profile's original `CustomPluginVariables.JSON` as evidence;
2. validate the entire document with PowerShell `ConvertFrom-Json`;
3. if malformed, rename it out of the active path and let rFactor 2 regenerate it;
4. re-enable only required plugins after confirming that the regenerated file is valid and the server is stable.

The active file may be under `UserData\player` or another profile selected with `+profile=<name>`. Do not assume the default profile.

When the shared-memory DLL is present but `" Enabled"` is missing or `0`, rFactor 2 may still load the native DLL briefly to inspect its exported plugin metadata, instantiate the plugin object, and enumerate custom variables. The plugin declares `" Enabled"` with a default of `0`, and its source intentionally does nothing when that variable is passed to `AccessCustomVariable(...)` because rFactor 2 owns the load/retain decision. With the plugin disabled, there is no evidence that rFactor 2 calls `Startup(...)` or session callbacks, so mapped-buffer creation and the callback-time internals-dump defect are not expected to be reachable. A loader-time fault is theoretically possible for any native DLL, but it is a poor match for a delayed session crash and was not the cause of the confirmed incident.

Relevant variables:

| Variable | Default in source | Meaning |
| --- | ---: | --- |
| `" Enabled"` | `0` | rFactor 2 plugin enable flag. Leading space is intentional. Must be `1`. |
| `DebugOutputLevel` | `0` | Controls `RF2SMMP_DebugOutput.txt`. Keep at `0` for initial isolation; after proving the log folder is writable, use `15` for startup/errors and `255` only for deeper tracing. |
| `DebugOutputSource` | `1` | Controls debug source mask. Set to `32767` for all sources while troubleshooting. |
| `DebugISIInternals` | `0` | Controls the example internals output files. Keep this at `0`; version 3.7.15.1 has an unsafe closed-file reuse path when it is enabled. |
| `DedicatedServerMapGlobally` | `0` | If `1`, dedicated-server maps are created as `Global\...<PID>`. Needs Windows permission. |
| `UnsubscribedBuffersMask` | `160` | Default unsubscribes Graphics and Weather only. Scoring remains enabled unless bit `2` is set. |

Important: unsubscribing a buffer stops updates, but `Startup()` still creates the maps. The source explicitly initializes all mapped buffers even when some are unsubscribed.

---

## Map Creation

In `Startup()`, the plugin calls `InitMappedBuffer(...)` for these output buffers:

* Telemetry
* Scoring
* Rules
* Multi Rules
* Force Feedback
* Graphics
* Pit Info
* Weather
* Extended

It also calls `InitMappedInputBuffer(...)` for input/control buffers:

* HWControl
* WeatherControl
* RulesControl
* PluginControl

`Extended` is initialized last and is treated by the plugin as the indicator that initialization completed.

If any `InitMappedBuffer(...)` call fails, `Startup()` returns early through `RETURN_IF_FALSE(...)`. With debug output enabled, failures should appear in `RF2SMMP_DebugOutput.txt`.

If startup succeeds, the debug output should include messages such as:

* `Starting rFactor 2 Shared Memory Map Plugin...`
* `DedicatedServerMapGlobally: ...`
* `UnsubscribedBuffersMask: ...`
* `Size of the Scoring buffer: ...`
* `Files mapped successfully.`

If those messages are absent and `DebugOutputLevel` is nonzero, `Startup()` likely did not run, did not read the expected config, or logging is being written somewhere else.

---

## Map Names

The map-name logic lives in `MappedBuffer::MapMemoryFile(...)`.

The plugin uses `GetModuleFileNameA(nullptr, moduleName, ...)` to inspect the process executable path.

If the executable path does **not** contain `Dedicated.exe`, it creates normal client map names such as:

```text
$rFactor2SMMP_Scoring$
```

If the executable path **does** contain `Dedicated.exe`, it appends the process ID:

```text
$rFactor2SMMP_Scoring$<PID>
```

If `DedicatedServerMapGlobally` is enabled, it uses:

```text
Global\$rFactor2SMMP_Scoring$<PID>
```

This PID suffix exists so multiple dedicated-server instances on one machine do not collide.

The current PoC probes all of those known names when `--pid <PID>` is supplied.

---

## Windows Namespace Implications

Without `Global\`, a named map is local to the Windows session that created it.

That matters because a dedicated server may run in a different Windows session/user from the terminal running Python. Examples:

* dedicated server launched as the same interactive user/session as Python: non-global PID-suffixed names should be openable;
* dedicated server launched as a service/session 0 while Python runs in a desktop/RDP session: non-global names may not be visible;
* dedicated server configured with `DedicatedServerMapGlobally = 1`: maps are created in the global namespace, but the server account needs the `Create Global Objects` permission.

Use this rule of thumb:

* If the PoC runs in the same Windows user/session as `Dedicated.exe`, prefer `DedicatedServerMapGlobally = 0`.
* If the PoC must run from a different Windows user/session, `DedicatedServerMapGlobally = 1` can make maps globally visible, but the account running `Dedicated.exe` must have the Windows `Create Global Objects` user right.
* If global map creation fails, use session-local maps (`DedicatedServerMapGlobally = 0`) from the same session, or grant the required Windows privilege before retrying global mode.

---

## Logging Behavior

There are three RF2SMMP log/output files of interest:

| File | Controlled by | Meaning |
| --- | --- | --- |
| `RF2SMMP_DebugOutput.txt` | `DebugOutputLevel` and `DebugOutputSource` | Main debug log. Empty if debug level/source masks do not include messages. |
| `RF2SMMP_InternalsTelemetryOutput.txt` | `DebugISIInternals` | Example telemetry/internals output. |
| `RF2SMMP_InternalsScoringOutput.txt` | `DebugISIInternals` | Example scoring/internals output. |

`DebugOutputLevel` defaults to `0`, so `RF2SMMP_DebugOutput.txt` can remain empty even when the DLL loads. A value of `1` enables only `Errors`. Startup/configuration lines use `CriticalInfo` (`2`). A value of `15` enables `Errors`, `CriticalInfo`, `DevInfo`, and `Warnings`, so it is enough to see startup configuration and map-creation failures. A value of `255` additionally enables synchronization, performance, timing, and verbose messages; use it only when deeper tracing is needed.

`WriteToAllExampleOutputFiles(...)` writes to the internals telemetry/scoring output files only when `DebugISIInternals` is enabled. Those files are useful evidence that the plugin object is receiving callbacks, but they do not by themselves prove that mapped files were created successfully.

Do **not** enable `DebugISIInternals` in the shipped 3.7.15.1 plugin. `WriteTelemetryInternals(...)` and `WriteScoringInternals(...)` cache their `FILE*`, close the local pointer after a callback, and leave the cached pointer non-null. The next callback can therefore write through a closed stream, and shutdown can close it again. The plugin runs in-process, so this undefined behavior can terminate `rFactor2 Dedicated.exe`. This is a source-level risk even though no matching upstream issue was found.

There is a second logging caveat: `WriteDebugMsg(...)` assumes `_fsopen(...)` succeeded in `setvbuf(...)` and multiple later `fprintf_s(...)` calls. Before temporarily enabling the main debug log, confirm that `UserData\Log` exists and is writable by the account launching the dedicated server. Leave all plugin logging off during the first crash-isolation run.

Safest crash-isolation settings:

```json
"DebugOutputLevel": 0,
"DebugOutputSource": 1,
"DebugISIInternals": 0
```

After the server is stable and `UserData\Log` has been verified writable, use `"DebugOutputLevel": 15`, `"DebugOutputSource": 32767`, and `"DebugISIInternals": 0` for startup/error diagnostics. Use `"DebugOutputLevel": 255` only if the normal startup/error output is not enough.

Restart rFactor 2 / `Dedicated.exe` after changing those settings.

---

## Monitor Application Behavior

The source monitor app constructs its mapped buffers with base names such as:

```text
$rFactor2SMMP_Scoring$
```

The monitor source does not appear to append `Dedicated.exe` PID values when opening maps. That means it can naturally detect client maps while failing to show dedicated-server maps, even if dedicated-server maps exist under PID-suffixed names.

So monitor success with a client and monitor failure with a dedicated server is not decisive. Use `python tools/rf2-poc/list_memory_maps.py --pid <PID>` or the PoC `--pid <PID>` path for dedicated-server testing.

If non-PID maps such as `$rFactor2SMMP_Scoring$` appear only while an rFactor 2 client is connected, those maps are most likely created by the client process, not by `Dedicated.exe`. They should not be treated as proof that the dedicated-server maps exist.

To prove dedicated-server startup/mapping, test with `Dedicated.exe` running and the client closed. Use `python tools/rf2-poc/list_memory_maps.py --pid <PID>` or the PoC `--pid <PID>` path.

---

## Scoring Publication

`UpdateScoring(...)` is the callback that writes live scoring data into the scoring map.

It does the following:

1. exits if maps are not initialized;
2. begins a scoring buffer update;
3. copies `ScoringInfoV01` into `mScoringInfo`;
4. copies up to `MAX_MAPPED_VEHICLES` `VehicleScoringInfoV01` entries into `mVehicles`;
5. sets `mBytesUpdatedHint` to the byte offset after the last copied vehicle;
6. ends the update;
7. updates Extended state.

If the map exists but no session/vehicles are active, consumers may see a valid map with zero or stale data. If no map exists at all, the failure is earlier than scoring publication: plugin enablement, startup, map creation, namespace, or permissions.

---

## Recommended Dedicated-Server Troubleshooting Sequence

1. Confirm the server process and PID:

   ```powershell
   Get-CimInstance Win32_Process | Where-Object { $_.Name -like '*Dedicated*.exe' } | Select-Object ProcessId, SessionId, Name, ExecutablePath, CommandLine
   ```

2. Confirm the dedicated-server profile/config has the plugin enabled:

   ```json
   " Enabled":1
   ```

3. Temporarily enable startup/error plugin debug:

   ```json
   "DebugOutputLevel":15,
   "DebugOutputSource":32767,
   "DebugISIInternals":0
   ```

   First prove that the server remains stable with all logging off. Only enable the main debug log after confirming that `UserData\Log` exists and is writable. Never enable `DebugISIInternals` in the shipped 3.7.15.1 binary.

4. Decide namespace mode:

   * same Windows session/user as Python: use `DedicatedServerMapGlobally = 0`;
   * different session/user/service: use `DedicatedServerMapGlobally = 1` and make sure the server account can create global objects.

5. Restart `Dedicated.exe`.

6. Check `RF2SMMP_DebugOutput.txt` for `Files mapped successfully.` and `Size of the Scoring buffer`.

7. Run the diagnostic:

   ```powershell
   python tools/rf2-poc/list_memory_maps.py --pid <PID>
   ```

8. If a scoring map is `OPEN`, run:

   ```powershell
   python tools/rf2-poc/run_poc.py --source shared-memory --pid <PID>
   ```

9. If no dedicated maps are visible/openable but debug output says files mapped successfully, the remaining suspect is Windows namespace/user/session visibility. Run Python from the same account/session as `Dedicated.exe`, or enable global mapping with the required permission.

10. If debug output does not say files mapped successfully, resolve the plugin startup/mapping error first.

---

## Current Assessment

Based on the source and current test observations:

* The investigated venue startup failure was caused by malformed `CustomPluginVariables.JSON` after a PC crash. Regenerating that file resolved it.
* The shared-memory plugin had not been enabled, so its normal startup/session callbacks and `DebugISIInternals` path were not implicated in that incident.
* The client path is viable because the PoC can open and display client shared-memory data.
* The dedicated-server path is viable when `DedicatedServerMapGlobally = 0` and the PoC runs in the same Windows session/user as `Dedicated.exe`.
* Global dedicated-server maps require the `Create Global Objects` Windows user right for the account running `Dedicated.exe`.
* For this project, a local dedicated server plus the Python PoC can be used as the first source-of-truth data path for leaderboard development.
* Normal read-only operation remains viable, but the 3.7.15.1 internals-dump and failed-debug-log paths are unsafe. Keep `DebugISIInternals` disabled and verify log-folder access before enabling the main debug log.
* Patching the DLL should remain a last resort unless an application dump identifies one of these plugin paths or a plugin-present/plugin-absent A/B test is conclusive.
