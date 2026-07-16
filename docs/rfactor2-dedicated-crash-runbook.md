# rFactor 2 Dedicated-Server Crash Runbook

Use this runbook on the affected Windows 10 host. It separates four questions that can otherwise be confused:

1. Is `rFactor2 Dedicated.exe` crashing, or is Windows bug-checking/restarting?
2. Does the crash require the shared-memory plugin?
3. Is the dedicated-server install/profile/content damaged?
4. Is the PC unstable independently of rFactor 2?

Two faults can coexist. A plugin can cause a user-mode rFactor 2 crash while an unrelated driver, storage, memory, power, or thermal fault causes the BSODs.

## Confirmed incident resolution

The cause of this incident was a corrupted active `CustomPluginVariables.JSON` after a PC crash. rFactor 2 Dedicated failed silently while reading that file during startup. Preserving/removing the corrupt file allowed rFactor 2 to regenerate a valid copy, after which the server started normally.

The shared-memory DLL had been copied into `Bin64\Plugins`, but its `" Enabled"` value was never set to `1`. It may have been loaded briefly so rFactor 2 could inspect its exports and enumerate custom variables, but the remembered configuration did not retain/activate it for normal startup and session callbacks. Consequently, the callback-time `DebugISIInternals` defect documented below was not the cause of this incident. The .NET runtime and Trackside were also unrelated to the confirmed failure.

### Safe recovery if this recurs

1. Stop rFactor 2 Dedicated completely.
2. Determine the active profile from the shortcut/process command line. `+profile=<name>` selects `UserData\<name>` instead of the default `UserData\player`.
3. Copy the active `CustomPluginVariables.JSON` to the USB evidence folder before changing it. Do not discard the corrupt original; it proves the failure and may identify the interrupted write.
4. Validate the file with `Get-Content '<path>\CustomPluginVariables.JSON' -Raw | ConvertFrom-Json`. A parse error confirms malformed JSON.
5. Rename the original to `CustomPluginVariables.JSON.corrupt-<date>.bak` rather than immediately deleting it.
6. Start rFactor 2 Dedicated once and verify that it regenerates `CustomPluginVariables.JSON` and remains running.
7. Stop it, inspect the regenerated file, and enable only the plugins actually required. Keep the shared-memory plugin disabled until a separate controlled test needs it.

Renaming/deleting the configuration is a repair action, not part of the evidence collector. Always preserve the original first.

## USB evidence collector

Use `tools\Collect-Rf2CrashEvidence.ps1` **before** modifying configuration or validating/reinstalling rFactor 2. It is compatible with stock Windows PowerShell 5.1 and requires no .NET Core runtime, Python, PowerShell modules, Sysinternals tools, or installation.

Safety properties:

- it never starts/stops a process or service;
- it never writes to the registry, event logs, rFactor 2 installation/profile, plugin configuration, firewall, dump settings, drivers, or Windows settings;
- its only writes are a new timestamped folder beside the script on the USB stick, or beneath an explicitly supplied `-OutputBase`;
- every evidence source is isolated, so missing classes, folders, privileges, and access-denied errors become warnings while collection continues;
- copies are size-capped, free space is checked before each copy, and incomplete destination copies are removed;
- dumps are inventoried by default and copied only with the explicit `-CopySmallDumps` switch.

Recommended use from an elevated **Windows PowerShell** window so protected event/dump sources are readable:

1. Copy only `Collect-Rf2CrashEvidence.ps1` from the repository's `tools` folder to the root of a writable USB stick.
2. Keep at least 512 MiB free on the stick when using `-CopySmallDumps`; the script itself preserves at least 25 MiB free and caps copied dumps at 256 MiB total.
3. Right-click **Windows PowerShell** and choose **Run as administrator**. Do not use Command Prompt for the command below.
4. Run the command, replacing the drive letters and installation path:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Collect-Rf2CrashEvidence.ps1" -Rf2Root "C:\Racing\rFactor2-Dedicated" -CopySmallDumps
```

Replace `E:` and the rFactor 2 path. `-ExecutionPolicy Bypass` applies only to this child PowerShell process; it does not change the machine's configured execution policy. If the installation path is unknown, omit `-Rf2Root`; auto-discovery still runs. Non-administrator execution is safe but may report access warnings for protected dumps or process modules.

Success is explicit: the output folder contains `COLLECTION-COMPLETE.txt`. Open `00-SUMMARY.txt` first and then `WARNINGS.txt`. A missing completion marker means the USB/output operation did not finish. Preserve the whole output directory.

## Current evidence

- On July 16, 2026, the 3.7.15.1 archive linked by the [upstream README](https://github.com/TheIronWolfModding/rF2SharedMemoryMapPlugin) was downloaded from its [published MediaFire location](https://www.mediafire.com/file/s6ojcr9zrs6q9ls/rf2_sm_tools_3.7.15.1.zip/file) and inspected in memory. Its extracted DLL and the vendored DLL both have 86,016 bytes, SHA-256 `9D98D77B767812DCA5AFEB6663F486B3AD5BE090C3E10783F36BC73A470AB5E6`, PE machine `0x8664` (AMD64), and PE linker timestamp March 25, 2023 12:37:19 UTC.
- It is a normal, non-AVX2 build. An old CPU lacking AVX2 is therefore not a concern for this exact DLL.
- It imports `MSVCR120.dll`, the x64 Visual C++ 2013 runtime. A modern .NET runtime does not provide that dependency. Upstream states that rFactor 2 normally supplies the VC12 runtime.
- Studio 397's dedicated-server guide explicitly launches the 64-bit executable from `Bin64\rFactor2 Dedicated.exe`. Crew Chief also states that 32-bit rFactor 2 is no longer supported. A normal current dedicated-server installation is therefore expected to be x64.
- Trackside opens the plugin's maps with read-only access. It does not write into rFactor 2, its profile, its plugin configuration, or its process.
- Incident evidence supplied by the operator: Trackside ran from USB and was not installed as a service. Its only persistent application-level changes were therefore the separately installed .NET runtime and the manually copied plugin.
- Incident resolution supplied by the operator: the active plugin configuration was malformed after a PC crash, and allowing rFactor 2 to regenerate it restored normal startup.
- The plugin source has two unsafe diagnostic paths:
  - `DebugISIInternals = 1` closes cached telemetry/scoring `FILE*` values but leaves them non-null, allowing later callbacks to reuse closed streams and shutdown to close them again.
    - Main debug logging assumes `_fsopen(...)` succeeded in `setvbuf(...)` and multiple later `fprintf_s(...)` calls.
- Both unsafe paths execute inside `rFactor2 Dedicated.exe`. `DebugISIInternals = 1` is therefore a credible explanation for a server that starts and then exits when scoring or telemetry callbacks begin.
- A rarer startup defect uses `char pid[8]` with unchecked `sprintf(...)` while building dedicated-server map names. It can overwrite the stack if the Windows process ID has eight or more decimal digits; record the actual PID before dismissing this path.

## Preserve evidence before repair

Before validating files, replacing profiles, running disk repair, uninstalling runtimes, or updating drivers:

1. Record the wall-clock time and exact symptom for one failure.
2. Copy the whole active `UserData` folder to safe storage.
3. Preserve these files if present:
   - `UserData\Log\trace.txt` and timestamped trace files;
   - `UserData\rFmini.dmp`;
   - the active profile's `CustomPluginVariables.JSON`, `player.JSON`, and `Multiplayer.JSON`;
   - `C:\Windows\Minidump\*.dmp` and `C:\Windows\MEMORY.DMP` for BSODs;
   - `%LOCALAPPDATA%\CrashDumps\*rFactor2*` for Windows Error Reporting dumps.
4. Export the Windows **Application** and **System** event logs for the relevant time window.
5. Record the dedicated-server executable path, command line, rFactor 2 version, active profile, selected race event, and plugin hash.

Do not use cleanup utilities before the evidence is copied. Full dumps can contain sensitive process memory; store and share them accordingly.

## Phase 1: Perform the decisive plugin A/B test

This A/B test is not needed for the resolved incident unless the server fails again with a valid regenerated `CustomPluginVariables.JSON`. If needed, use the same shortcut, profile, event, content, and observation period in each run. Keep Trackside stopped for all baseline runs.

### Run A — plugin absent

1. Stop the dedicated server.
2. Rename `Bin64\Plugins\rFactor2SharedMemoryMapPlugin64.dll` to `rFactor2SharedMemoryMapPlugin64.dll.disabled`. Do not delete it yet.
3. Launch the same server configuration that normally fails.
4. Let it run beyond the usual failure time and, if possible, enter the same session state.
5. Record whether it stays running, exits, hangs, or causes a BSOD.

### Run B — plugin present with safe settings

1. Restore the exact DLL name.
2. Find the `CustomPluginVariables.JSON` belonging to the profile that the shortcut actually launches. A `+profile=...` command-line argument selects a non-default profile.
3. Preserve a copy, validate that it is valid JSON, and set the plugin values to a minimal safe state:

   ```json
   " Enabled": 1,
   "DebugOutputLevel": 0,
   "DebugOutputSource": 1,
   "DebugISIInternals": 0,
   "DedicatedServerMapGlobally": 0,
   "EnableDirectMemoryAccess": 0,
   "EnableHWControlInput": 0,
   "EnableWeatherControlInput": 0,
   "EnableRulesControlInput": 0,
   "UnsubscribedBuffersMask": 160
   ```

4. Launch the same server configuration.
5. While it is running, prove that the plugin actually loaded. In elevated 64-bit PowerShell, enumerate the process modules and look for the exact DLL:

   ```powershell
   $server = Get-CimInstance Win32_Process |
       Where-Object { $_.Name -in @('rFactor2 Dedicated.exe', 'Dedicated.exe') } |
       Select-Object -First 1
   (Get-Process -Id $server.ProcessId).Modules |
       Where-Object ModuleName -eq 'rFactor2SharedMemoryMapPlugin64.dll' |
       Select-Object ModuleName, FileName, FileVersionInfo
   ```

    If module enumeration is blocked, prove successful startup by opening a PID-suffixed map from the same Windows session with `python tools/rf2-poc/list_memory_maps.py --pid <PID>`. The plugin creates all maps during startup even when updates are unsubscribed. If neither method proves loading, do not interpret a stable Run B as evidence that the plugin is safe.
6. Continue for the same observation period as Run A.
7. Keep Trackside stopped. The plugin does not need a consumer in order to publish maps.

### Interpret the A/B result

| Result | Interpretation |
| --- | --- |
| Run A stable; Run B repeatedly crashes | Plugin or plugin configuration is strongly implicated. Capture the application fault/dump before changing anything else. |
| Run A and Run B both crash the same way | The plugin is not required for the failure. Move to install/profile/content and machine checks. |
| Run A and Run B both BSOD | Treat this primarily as a Windows kernel, driver, hardware, firmware, power, thermal, or storage problem. |
| Run B stable with `DebugISIInternals = 0` but fails with the old configuration | Diff the configuration. If the old value was `DebugISIInternals = 1`, the source-level closed-stream bug is a strong suspect. Confirm it with a single-variable repro or dump stack if safe; do not enable it again in normal use. |
| Run B stable until Trackside starts | Capture CPU, memory, disk, and an application dump. This would be unexpected because Trackside reads maps only; do not assume causality from timing alone. |

A single successful run is weak evidence. Repeat the failing and passing variants at least twice if the PC remains safe enough to test.

## Phase 2: Prove architecture and plugin identity

While the server is running, record its actual executable and command line:

```powershell
Get-CimInstance Win32_Process |
    Where-Object { $_.Name -in @('rFactor2 Dedicated.exe', 'Dedicated.exe') } |
    Select-Object ProcessId, SessionId, ExecutablePath, CommandLine
```

The expected path is under `Bin64`. To inspect a PE file directly in PowerShell:

```powershell
$path = 'C:\path\to\rFactor2 Dedicated.exe'
$bytes = [IO.File]::ReadAllBytes($path)
$pe = [BitConverter]::ToInt32($bytes, 0x3c)
'0x{0:X4}' -f [BitConverter]::ToUInt16($bytes, $pe + 4)
```

Interpret `0x8664` as AMD64/x64 and `0x014C` as x86. Run the same check against the plugin and calculate its hash:

```powershell
Get-FileHash 'C:\path\to\Bin64\Plugins\rFactor2SharedMemoryMapPlugin64.dll' -Algorithm SHA256
```

If the server executable is `0x8664`, the vendored plugin has the correct bitness. If an unexpected legacy x86 executable is actually being launched, do not load this x64 plugin; correct the shortcut/install and migrate to the supported `Bin64` server.

A bitness mismatch normally fails during DLL loading with a bad-image error. It is a weaker match for a server that runs for a while and exits only after session callbacks begin.

## Phase 3: Capture an rFactor 2 application crash

### Enable rFactor 2 trace logging

Edit the shortcut used to start Dedicated Server. Preserve all existing arguments and append `+trace=2 +traceFlush`. A complete target normally resembles:

```text
"C:\Racing\rFactor2-Dedicated\Bin64\rFactor2 Dedicated.exe" +path=".." +profile=player +trace=2 +traceFlush
```

Omit `+profile=player` if the normal shortcut does not specify a profile; never remove the existing `+path` or profile argument. Studio 397 recommends `+traceFlush` for crashes so each line reaches disk immediately.

After reproducing the failure, do **not** launch the server again before preserving the latest evidence. Copy:

- `<rFactor2 root>\UserData\Log\trace.txt` and the newest timestamped trace files;
- `<rFactor2 root>\UserData\rFmini.dmp`, if its timestamp matches the failure;
- `<rFactor2 root>\UserData\Log\RF2SMMP_DebugOutput.txt`, if plugin debug logging had deliberately been enabled;
- the active profile's `CustomPluginVariables.JSON`, including a malformed original.

Inspect the final 50–200 lines of `trace.txt`. Look for the last successfully loaded component, JSON/configuration parsing, `CustomPluginVariables`, plugin enumeration, missing package/content messages, and whether shutdown is orderly or abrupt. A trace ending at configuration loading with no crash event can indicate a handled/silent startup abort; WER will not necessarily create a dump for an orderly exit.

### Application Event Log

Immediately after the failure, inspect **Event Viewer → Windows Logs → Application** for events 1000 and 1001. Record:

- faulting application;
- faulting module;
- exception code;
- fault offset;
- application and module versions;
- report ID and dump path.

A quick PowerShell view is:

```powershell
$since = (Get-Date).AddDays(-7)
Get-WinEvent -FilterHashtable @{ LogName='Application'; StartTime=$since; Id=1000,1001 } |
    Where-Object Message -match 'rFactor2|Dedicated|rFactor2SharedMemoryMapPlugin' |
    Select-Object TimeCreated, ProviderName, Id, Message |
    Format-List
```

For a support-quality manual export, open **Event Viewer → Windows Logs → Application**, choose **Filter Current Log**, select the failure time range and event IDs `1000,1001`, then choose **Save Filtered Log File As** and save an `.evtx` file to the USB evidence folder. Repeat under **Windows Logs → System** for IDs `41,1001,6008,7,51,55,98,129,153,161`. Also run `perfmon.exe /rel` to view Reliability Monitor and record the matching critical events. Do not clear either event log.

Useful interpretations include:

| Evidence | Meaning |
| --- | --- |
| Faulting module is `rFactor2SharedMemoryMapPlugin64.dll` | Direct plugin evidence. Keep the dump and exact plugin configuration. |
| Faulting module is `MSVCR120.dll` | Could still originate in plugin CRT/file handling; inspect the stack in the dump. |
| Exception `0xC000001D` | Illegal instruction. This exact vendored DLL is not an AVX2 build, so investigate another module or damaged hardware/code. |
| Exception `0xC000007B`, `0xC000012F`, or missing-module message | Bad image, bitness, or dependency/loading problem; verify PE architecture and VC++ 2013 x64 runtime. |
| Exception `0xC0000005` | Access violation. The faulting stack/module is needed; the code alone is not enough to assign blame. |
| Exception `0xC0000409` | Fast-fail/stack-buffer/CRT termination. Plugin logging, the fixed-size PID buffer, and the stack deserve attention. |
| No application error or dump | The launcher/server may be exiting intentionally due to configuration/content, or a custom crash handler may intercept it. Use the flushed rFactor 2 trace and termination monitoring. |

### Optional per-application WER dump

Windows 10 can collect user-mode dumps through built-in Windows Error Reporting. This changes dump settings, so run the USB collector first. Use the exact image name returned by the Phase 2 process query; supported configurations may report either `rFactor2 Dedicated.exe` or `Dedicated.exe`.

1. Ensure the system drive has free space. Use a local fixed-disk folder, not the USB stick, for live dump creation.
2. In elevated Windows PowerShell, create a **mini-dump** configuration for the exact executable:

    ```powershell
    $imageName = 'rFactor2 Dedicated.exe'
    $dumpFolder = 'C:\CrashDumps\rFactor2'
    $key = "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\$imageName"
    New-Item -ItemType Directory -Path $dumpFolder -Force | Out-Null
    New-Item -Path $key -Force | Out-Null
    New-ItemProperty -Path $key -Name DumpFolder -PropertyType ExpandString -Value $dumpFolder -Force | Out-Null
    New-ItemProperty -Path $key -Name DumpCount -PropertyType DWord -Value 5 -Force | Out-Null
    New-ItemProperty -Path $key -Name DumpType -PropertyType DWord -Value 1 -Force | Out-Null
    ```

3. Reproduce one crash and check `C:\CrashDumps\rFactor2` immediately. Copy any new dump to the USB evidence folder.
4. If a mini dump does not contain enough stack/memory evidence and sufficient disk space exists, change only `DumpType` to `2` for a full dump and reproduce once. Full dumps can be large.
5. After diagnosis, remove the per-image key **only if it did not exist before this procedure**:

    ```powershell
    Remove-Item -LiteralPath $key -Recurse -Force
    ```

If the collector showed pre-existing values for that key, restore those values instead of deleting it. WER captures unhandled crashes, not every clean/process-controlled exit, and an application's own crash handler can prevent collection. The confirmed malformed-JSON startup failure may therefore produce no WER dump.

The equivalent registry location is:

```text
HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\<exact image name>
```

### Analyze an application dump

Analyze `rFmini.dmp` or a WER dump on a separate stable PC with WinDbg. Open the dump, then run:

```text
.symfix
.reload
!analyze -v
k
lm
lmvm rFactor2SharedMemoryMapPlugin64
```

The plugin-specific `lmvm` command can report no match when that DLL was not loaded; that is useful evidence. Preserve the full debugger text. Focus on `EXCEPTION_CODE`, `MODULE_NAME`, `IMAGE_NAME`, `FAILURE_BUCKET_ID`, and `STACK_TEXT`. A module named as faulting is a lead, while multiple plugin frames on the failing stack provide much stronger attribution.

## Phase 4: Check the plugin configuration and dependency

### Locate every possible plugin configuration

```powershell
Get-ChildItem 'C:\path\to\rFactor2-Dedicated' -Filter CustomPluginVariables.JSON -Recurse -ErrorAction SilentlyContinue |
    Select-Object FullName, LastWriteTime, Length
```

Validate each candidate without rewriting it:

```powershell
Get-Content 'C:\path\to\CustomPluginVariables.JSON' -Raw | ConvertFrom-Json | Out-Null
```

Confirm which profile is active from the process command line. Do not assume the default `UserData\player` file is the one in use.

Keep `DebugISIInternals` at `0`. If main plugin logging is later needed, first verify that the active installation's `UserData\Log` directory exists and is writable by the server account. Then use `DebugOutputLevel = 15`, `DebugOutputSource = 32767`, and still keep `DebugISIInternals = 0`.

### Verify Visual C++ 2013 x64

The exact DLL imports `MSVCR120.dll`. Check the x64 copy and version:

```powershell
Get-Item "$env:WINDIR\System32\msvcr120.dll" -ErrorAction SilentlyContinue |
    Select-Object FullName, Length, @{Name='FileVersion';Expression={$_.VersionInfo.FileVersion}}
```

If it is absent or an application/loader event identifies it, install or repair the official **Microsoft Visual C++ 2013 x64 Redistributable**. Do not download loose runtime DLLs from third-party DLL sites. A missing dependency usually prevents plugin load; it does not explain a BSOD.

## Phase 5: Test for rFactor 2 file/profile corruption

Preserve `UserData`, `Packages`, and any locally managed content first.

1. Run SteamCMD validation against the existing dedicated-server installation:

   ```text
   steamcmd +login anonymous +force_install_dir C:\path\to\rFactor2-Dedicated +app_update 400300 validate +quit
   ```

2. Retest with the plugin absent.
3. If it still fails, test a clean profile or a separate clean dedicated-server installation rather than repeatedly modifying the only copy.
4. Use a minimal known-good race event and official content. If only one event/car/track combination fails, isolate content rather than the executable.
5. Compare the active JSON files with the preserved copies and validate their syntax.

A separate clean install is the strongest corruption test: same PC and drivers, but fresh official binaries and fresh profile. If both old and clean installs fail identically with the plugin absent, file corruption becomes much less likely.

## Phase 6: Treat BSODs as a separate high-priority fault

A normal user-mode DLL or .NET process cannot directly issue a Windows bug check. It can expose a latent kernel driver or hardware fault through load, but BSOD diagnosis must use the kernel dump. Repeated BSODs also make subsequent application/file corruption plausible.

### Preserve and inspect BSOD evidence

- Copy `C:\Windows\Minidump\*.dmp` and `C:\Windows\MEMORY.DMP`.
- In **Event Viewer → Windows Logs → System**, find `BugCheck` event 1001 around the restart.
- `Kernel-Power` event 41 only proves the shutdown was not clean; it is not the root cause by itself.
- Inspect WHEA, disk, storage-controller, NTFS, and display-driver events around the same timestamp.
- Open each dump in WinDbg and run `!analyze -v`.

If `C:\Windows\Minidump` remains empty after a BSOD:

1. open **System Properties → Advanced → Startup and Recovery → Settings**;
2. set **Write debugging information** to **Small memory dump (256 KB)**;
3. leave the dump directory as `%SystemRoot%\Minidump`;
4. ensure the Windows-managed page file remains enabled on the system drive and that the drive has free space;
5. after another BSOD, inspect System events from `volmgr`, especially event 161, for dump-creation failures.

This configuration change is separate from the read-only collector. Configure it before a future failure only if existing BSOD dumps are absent.

PowerShell overview:

```powershell
$since = (Get-Date).AddDays(-30)
Get-WinEvent -FilterHashtable @{ LogName='System'; StartTime=$since } |
    Where-Object {
        $_.ProviderName -match 'BugCheck|WHEA|Disk|Ntfs|storahci|stornvme|Display' -or
        $_.Id -in 41,51,55,98,129,153,6008,1001
    } |
    Select-Object TimeCreated, ProviderName, Id, LevelDisplayName, Message |
    Format-List
```

Interpret patterns, not only the blamed filename:

- `WHEA_UNCORRECTABLE_ERROR (0x124)` or WHEA hardware records: CPU, memory controller, PCIe device, motherboard, power, cooling, BIOS, or overclocking are primary suspects.
- `MEMORY_MANAGEMENT`, `PFN_LIST_CORRUPT`, `FAULTY_HARDWARE_CORRUPTED_PAGE`, or varying random blamed modules: test RAM and CPU/memory stability; disable overclock/XMP temporarily.
- `KERNEL_DATA_INPAGE_ERROR`, `UNEXPECTED_STORE_EXCEPTION`, NTFS errors, or storage-controller resets: prioritize the system drive, cabling, controller/firmware, SMART/vendor diagnostics, and file-system integrity.
- Repeated identical third-party `.sys` driver in reliable stacks: update, roll back, or remove that driver after preserving versions and dumps.
- `VIDEO_TDR_FAILURE` or display stack evidence: investigate GPU driver, GPU stability, power, and thermals even if the server itself is mostly headless.

For a BSOD minidump, run `.symfix`, `.reload`, `!analyze -v`, `.bugcheck`, `k`, and `lm` in WinDbg. Preserve `BUGCHECK_CODE`, all arguments, `MODULE_NAME`, `IMAGE_NAME`, `FAILURE_BUCKET_ID`, and `STACK_TEXT`. `ntoskrnl.exe` appearing in a stack is not by itself a root cause; compare multiple dumps for a repeated third-party driver or a consistent hardware pattern.

Before stress testing, check cooling, power, storage health, and backups. Record BIOS, chipset, storage, network, and graphics driver versions before changing them. Use the drive vendor's diagnostics and a multi-pass memory test; Windows Memory Diagnostic is only a basic first pass. Run `chkdsk /scan` before considering an offline repair. Do not run aggressive stress tests on a machine that is overheating or losing storage data.

## Phase 7: Assess the .NET runtime change

Trackside targets `net10.0-windows` and the deployment is framework-dependent, so a .NET/ASP.NET Core 10 runtime prompt is expected. Modern .NET installs side-by-side under `Program Files\dotnet`; rFactor 2 and the native plugin do not reference it.

- The plugin depends on VC++ 2013 `MSVCR120.dll`, not .NET.
- Installing .NET does not change the plugin's bitness.
- A .NET runtime installation is a low-probability explanation for a native rFactor 2 application crash and a very low-probability explanation for a BSOD.
- Current .NET 10 support on Windows 10 is limited to listed Enterprise/LTSC variants. An ordinary Windows 10 Home/Pro 22H2 machine is out of normal OS support in 2026, but that support status does not by itself establish crash causation.

Inventory the installed runtimes:

```powershell
dotnet --list-runtimes
```

Do not uninstall .NET as the first experiment; the plugin A/B test is much more discriminating. If a controlled rollback is still desired later, record the exact installed products and versions and remove them through **Apps & features**, then repeat the already stable plugin-absent baseline. Uninstalling .NET will not repair damaged rFactor 2 files.

## Recommended order onsite

1. Run the USB collector before changing anything; preserve its complete output folder.
2. Preserve and validate the active `CustomPluginVariables.JSON`.
3. If malformed, rename it out of the active path, let rFactor 2 regenerate it, and confirm stable startup. This resolved the investigated incident.
4. Keep `+trace=2 +traceFlush` available for any recurrence and preserve `trace.txt` before restarting again.
5. Continue collecting/analyzing the pre-existing BSOD minidumps separately; the repaired rFactor 2 startup does not explain or repair the machine crashes.
6. Use WER application dumps only for a genuine unhandled rFactor 2 process crash. Silent/handled configuration aborts may not generate one.
7. Use plugin A/B isolation only if the server fails again with valid regenerated configuration.
8. Do not resume venue use until recurring BSODs have a credible dump-based diagnosis; an apparently fixed rFactor 2 process does not make the machine reliable.

## Evidence that would make the diagnosis conclusive

The strongest package is:

- repeated A/B result using identical server inputs;
- exact active plugin JSON and command line;
- application event 1000/1001 details;
- `trace.txt` with `+traceFlush`;
- timestamp-matched `rFmini.dmp` or WER full dump analyzed with `!analyze -v`;
- timestamp-matched Windows BSOD minidump analyzed separately;
- SteamCMD validation result and clean-profile/install result;
- plugin and executable architecture/hash.

With those artifacts, plugin fault, dependency/load fault, rFactor 2 content/profile corruption, and machine instability can normally be separated rather than guessed from coincidence.
