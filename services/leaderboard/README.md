# services/leaderboard

Reads the rF2 shared-memory scoring buffer, applies the "counts for
leaderboard" flag, stores qualifying results, serves them to /web/kiosk.

Covers implementation-plan.md Phase 1.

## Phase 0A PoC

The quick live-data proof of concept lives in `poc/`.

It is a Python standard-library HTTP server plus a small rFactor 2 scoring reader.
No `pip install` is required for the current PoC.

For live shared-memory mode, run the PoC on the Windows machine that can see the
rFactor 2 shared-memory map. For dedicated-server testing this normally means
the same host that runs `Dedicated.exe`, unless the shared-memory plugin is
configured to create globally visible maps and permissions allow access.

The PoC does not create rFactor 2 shared memory itself. Live mode requires
`TheIronWolfModding/rF2SharedMemoryMapPlugin` to be installed and loaded in
rFactor 2 / `Dedicated.exe`. Mock mode bypasses rFactor 2 and shared memory
entirely and uses `poc/fixtures/mock_scoring_snapshot.json`.

After copying the plugin DLL into `Bin64\Plugins`, start rFactor 2 or
`Dedicated.exe` once so `CustomPluginVariables.json` is updated, then set the
plugin's `" Enabled"` value to `1` and restart. The leading space in
`" Enabled"` is intentional in rFactor 2 plugin configuration.

For dedicated-server live mode, pass the current `Dedicated.exe` PID:

```powershell
Get-Process Dedicated | Select-Object Id, ProcessName, Path
python services/leaderboard/poc/run_poc.py --source shared-memory --pid <PID>
```

If live mode cannot open a map, inspect visible/probed map names:

```powershell
python services/leaderboard/poc/list_memory_maps.py --pid <PID>
```

Run with mock data from the repository root:

```powershell
python services/leaderboard/poc/run_poc.py --source mock
```

Then open `http://127.0.0.1:8877/poc`.

### Telemetry recording analysis

Analyze stored session telemetry recordings and generate cadence/quality metrics:

```powershell
python services/leaderboard/poc/analyze_recordings.py services/leaderboard/poc/tests/data/bahrain-gp-2014-practice-ac00312535 --json > analysis.json
```

The analyzer reads `telemetry_samples.jsonl` and `report.json` from each session folder, produces a text summary by default, and can emit machine-readable JSON with `--json`.

See `docs/core-poc.md` for detailed host setup, plugin requirements,
shared-memory explanation, mock boundaries, live-mode commands, and
troubleshooting.

See `docs/telemetry-report-poc-plan.md` for telemetry report findings,
captured-data cadence measurements, and the final PoC decision plan for
central server collection versus rig-local telemetry collectors.

See `docs/shared-memory-plugin-investigation.md` for the source-level analysis
of how `rF2SharedMemoryMapPlugin` creates maps, logs startup, and handles
dedicated-server map names.
