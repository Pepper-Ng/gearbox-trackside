# rF2 PoC Tools

This folder contains the Phase 0A Python proof of concept for rFactor 2 shared-memory reading, browser delivery, telemetry capture, and analysis.

It is a Python standard-library HTTP server plus rFactor 2 scoring/telemetry readers. No `pip install` step is currently required.

For live shared-memory mode, run the PoC on the Windows machine that can see the rFactor 2 shared-memory maps. For dedicated-server testing this normally means the same host that runs `Dedicated.exe`, unless the shared-memory plugin is configured to create globally visible maps and permissions allow access.

The PoC does not create rFactor 2 shared memory itself. Live mode requires `TheIronWolfModding/rF2SharedMemoryMapPlugin` to be installed and loaded in rFactor 2 or `Dedicated.exe`. Mock mode bypasses rFactor 2 and shared memory entirely and uses `fixtures/mock_scoring_snapshot.json`.

After copying the plugin DLL into `Bin64\Plugins`, start rFactor 2 or `Dedicated.exe` once so `CustomPluginVariables.json` is updated, then set the plugin's `" Enabled"` value to `1` and restart. The leading space in `" Enabled"` is intentional in rFactor 2 plugin configuration.

Run with mock data from the repository root:

```powershell
python tools\rf2-poc\run_poc.py --source mock
```

Then open `http://127.0.0.1:8877/poc`.

For dedicated-server live mode, pass the current `Dedicated.exe` PID:

```powershell
Get-Process Dedicated | Select-Object Id, ProcessName, Path
python tools\rf2-poc\run_poc.py --source shared-memory --pid <PID>
```

If live mode cannot open a map, inspect visible/probed map names:

```powershell
python tools\rf2-poc\list_memory_maps.py --pid <PID>
```

Analyze stored session telemetry recordings and generate cadence/quality metrics:

```powershell
python tools\rf2-poc\analyze_recordings.py tools\rf2-poc\tests\data\bahrain-gp-2014-practice-ac00312535 --json > analysis.json
```

The analyzer reads `telemetry_samples.jsonl` and `report.json` from each session folder, produces a text summary by default, and can emit machine-readable JSON with `--json`.

Useful checks:

```powershell
python -m compileall -q tools\rf2-poc
python -m unittest discover tools\rf2-poc\tests
```

See `docs/core-poc.md` for detailed host setup, plugin requirements, shared-memory explanation, mock boundaries, live-mode commands, and troubleshooting.

See `docs/telemetry-report-poc-plan.md` for telemetry report findings, captured-data cadence measurements, and the final PoC decision plan for central server collection versus rig-local telemetry collectors.

See `docs/shared-memory-plugin-investigation.md` for the source-level analysis of how `rF2SharedMemoryMapPlugin` creates maps, logs startup, and handles dedicated-server map names.