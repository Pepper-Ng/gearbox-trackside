# rF2 Shared-Memory Plugin Investigation

This note summarizes the source-level behavior of `TheIronWolfModding/rF2SharedMemoryMapPlugin` as vendored under `memorymap/src`.

The practical goal is to understand why a normal rFactor 2 client can expose readable shared-memory data while `Dedicated.exe` appears to load the DLL but does not expose maps that the PoC can open.

---

## High-Level Conclusion

The plugin source says that shared-memory maps are created during `SharedMemoryPlugin::Startup()`, not lazily after the first vehicle joins. If startup completes successfully, the maps should exist even before useful scoring data is written.

Therefore:

* Client mode working proves the PoC can open and decode a real plugin-created map.
* Dedicated-server `OpenFileMappingW(... Windows error 2)` means none of the tried dedicated-server map names exists from the PoC process's Windows namespace.
* Log files appearing under `UserData\Log` strongly suggests the DLL was loaded and at least some plugin code ran, but it does not by itself prove that mapped files were successfully created.
* The next decisive check is `list_memory_maps.py --pid <PID>` plus plugin debug output with `DebugOutputLevel` enabled.

No source patch to the DLL is justified yet. The public plugin is widely used, and the source path indicates this should work once namespace/config/startup details are understood.

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

Relevant variables:

| Variable | Default in source | Meaning |
| --- | ---: | --- |
| `" Enabled"` | `0` | rFactor 2 plugin enable flag. Leading space is intentional. Must be `1`. |
| `DebugOutputLevel` | `0` | Controls `RF2SMMP_DebugOutput.txt`. Set to `255` for all levels while troubleshooting. |
| `DebugOutputSource` | `1` | Controls debug source mask. Set to `32767` for all sources while troubleshooting. |
| `DebugISIInternals` | `0` | Controls `RF2SMMP_InternalsTelemetryOutput.txt` and `RF2SMMP_InternalsScoringOutput.txt`. |
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

So if non-global dedicated maps cannot be opened, turning `DedicatedServerMapGlobally` **on** is the useful cross-session test. Turning it off is only useful when both processes are definitely in the same Windows session and global creation is failing.

---

## Logging Behavior

There are three RF2SMMP log/output files of interest:

| File | Controlled by | Meaning |
| --- | --- | --- |
| `RF2SMMP_DebugOutput.txt` | `DebugOutputLevel` and `DebugOutputSource` | Main debug log. Empty if debug level/source masks do not include messages. |
| `RF2SMMP_InternalsTelemetryOutput.txt` | `DebugISIInternals` | Example telemetry/internals output. |
| `RF2SMMP_InternalsScoringOutput.txt` | `DebugISIInternals` | Example scoring/internals output. |

`DebugOutputLevel` defaults to `0`, so `RF2SMMP_DebugOutput.txt` can remain empty even when the DLL loads.

`WriteToAllExampleOutputFiles(...)` writes to the internals telemetry/scoring output files only when `DebugISIInternals` is enabled. It writes `-STARTUP-` before mapped-buffer initialization completes. Therefore these files are evidence that the plugin reached that code path, but they do not prove maps were created successfully.

Recommended troubleshooting settings:

```json
"DebugOutputLevel": 255,
"DebugOutputSource": 32767,
"DebugISIInternals": 1
```

Restart rFactor 2 / `Dedicated.exe` after changing those settings.

---

## Monitor Application Behavior

The source monitor app constructs its mapped buffers with base names such as:

```text
$rFactor2SMMP_Scoring$
```

The monitor source does not appear to append `Dedicated.exe` PID values when opening maps. That means it can naturally detect client maps while failing to show dedicated-server maps, even if dedicated-server maps exist under PID-suffixed names.

So monitor success with a client and monitor failure with a dedicated server is not decisive. Use `list_memory_maps.py --pid <PID>` or the PoC `--pid <PID>` path for dedicated-server testing.

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

3. Temporarily enable verbose plugin debug:

   ```json
   "DebugOutputLevel":255,
   "DebugOutputSource":32767,
   "DebugISIInternals":1
   ```

4. Decide namespace mode:

   * same Windows session/user as Python: try `DedicatedServerMapGlobally = 0` first;
   * different session/user/service: set `DedicatedServerMapGlobally = 1` and make sure the server account can create global objects.

5. Restart `Dedicated.exe`.

6. Check `RF2SMMP_DebugOutput.txt` for `Files mapped successfully.` and `Size of the Scoring buffer`.

7. Run the diagnostic:

   ```powershell
   python services/leaderboard/poc/list_memory_maps.py --pid <PID>
   ```

8. If a scoring map is `OPEN`, run:

   ```powershell
   python services/leaderboard/poc/run_poc.py --source shared-memory --pid <PID>
   ```

9. If no dedicated maps are visible/openable but debug output says files mapped successfully, the remaining suspect is Windows namespace/user/session visibility. Try running Python from the same account/session as `Dedicated.exe`, or enable global mapping with the required permission.

10. If debug output does not say files mapped successfully, resolve the plugin startup/mapping error first.

---

## Current Assessment

Based on the source and current test observations:

* The client path is proven viable because the PoC can open and display client shared-memory data.
* Dedicated-server logs appearing means the DLL is probably loaded, but does not prove map creation.
* Dedicated-server `Windows error 2` after probing PID-suffixed names means the expected maps are not visible/openable from Python.
* The next best evidence is the new `list_memory_maps.py --pid <PID>` output and a non-empty debug log with `DebugOutputLevel = 255`.
* Patching the DLL should remain a last resort. The likely issue is configuration, namespace/session visibility, or server startup/map initialization rather than a fundamentally broken plugin.
