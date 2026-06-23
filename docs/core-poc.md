# Phase 0A Core Live-Data PoC

This document records the completed Phase 0A proof of concept: what useful rFactor 2 scoring and telemetry data can reach a browser with minimal software.

Phase 0A intentionally did not prove the final leaderboard architecture, staff controls, durable persistence, deployment packaging, printing, camera behavior, or venue hardening. It proved the live data path and captured the implementation findings needed for the next phase.

Related docs:

* `docs/shared-memory-plugin-investigation.md` - source-level plugin behavior, map naming, namespace rules, and deeper troubleshooting.
* `docs/telemetry-report-poc-plan.md` - final telemetry-source decision and report/analytics implementation requirements.
* `docs/implementation-plan.md` - next implementation phases.

---

## PoC Outcome

Go decision: proceed to implementation.

Resolved findings:

* Dedicated-server shared memory is viable for the live leaderboard source.
* Server-published telemetry is viable for telemetry report capture.
* Shared memory is a live snapshot source, not a completed-session archive. Future services must record lap, sector, scoring, and telemetry values as they appear.
* Final telemetry collection policy and source-modularity requirements live in `docs/telemetry-report-poc-plan.md`.

The validated runtime shape is:

```text
[Windows host running rFactor 2 or Dedicated.exe]
  rFactor 2 / Dedicated.exe
  + rF2SharedMemoryMapPlugin
      |
      | Windows named shared-memory maps
      v
  PoC process
      |
      | local HTTP
      v
  Browser at http://127.0.0.1:8877/poc
```

The PoC process must run on a Windows account/session that can see the rFactor 2 shared-memory maps. The browser may be local or on the LAN if the PoC binds to a non-loopback interface.

---

## Source Facts To Preserve

Live mode requires `TheIronWolfModding/rF2SharedMemoryMapPlugin`; Python does not read rFactor 2 by itself.

Important map names:

| Process | Scoring map | Telemetry map |
| --- | --- | --- |
| rFactor 2 client | `$rFactor2SMMP_Scoring$` | `$rFactor2SMMP_Telemetry$` |
| Dedicated server | `$rFactor2SMMP_Scoring$<PID>` | `$rFactor2SMMP_Telemetry$<PID>` |
| Dedicated server global namespace | `Global\$rFactor2SMMP_Scoring$<PID>` | `Global\$rFactor2SMMP_Telemetry$<PID>` |

Dedicated-server maps are PID-suffixed. A monitor tool that only opens base client names may fail against `Dedicated.exe` even when dedicated-server maps exist.

Plugin configuration facts that mattered during the PoC:

* The plugin entry in `CustomPluginVariables.json` must have its rFactor 2 `" Enabled"` value set to `1`; the leading space is intentional.
* `UnsubscribedBuffersMask` must not disable scoring (`2`) or telemetry if telemetry capture is expected.
* Non-global dedicated-server maps are visible only in the Windows namespace/session that created them.
* `DedicatedServerMapGlobally = 1` can expose maps globally, but the account running `Dedicated.exe` needs the Windows `Create Global Objects` user right.

---

## Available Data

The dedicated-server path exposed the project-critical live fields:

* session state, track, session type, game phase, realtime state, vehicle count, current/end session time;
* scoring rows for current vehicles, including driver/vehicle name, laps, place, best lap, sector timing, current lap, current sector, lap distance, track percent, gaps, finish status, and race order fields where rFactor 2 exposes them;
* weather/environment fields where present, including temperatures, rain/cloud/wetness, and wind;
* flag/yellow state where scoring exposes it;
* all-car telemetry rows joined to scoring rows by vehicle ID;
* telemetry channels including throttle, brake, steering, gear, speed, local velocity/acceleration, G-force axes and magnitude, engine RPM, tire/fuel/impact-related fields where exposed.

Important interpretation notes:

* Scoring remains the required live source. Telemetry supplements scoring.
* Lap percent / lap distance currently come from scoring, while high-rate throttle/brake/steer/gear/speed/G-force come from telemetry.
* If scoring updates slower than telemetry, high-rate telemetry samples can have repeated lap-percent values while telemetry channels still change.
* Some rFactor 2 sector values are cumulative; the PoC derives split values when enough data is available.
* Report generation needs a recorder because full historical lap/sector/telemetry archives are not emitted as one completed-session object.

---

## Telemetry Cadence Evidence

Earlier Bahrain captures kept under `tools/rf2-poc/tests/data/` showed that the older combined collector did not preserve stable 50 Hz per-driver telemetry:

* Practice: 48,181 stored telemetry samples, 24 proper laps, 10 excluded laps; proper-lap rates ranged from about 1.4 Hz to 14.2 Hz, with an 8.2 Hz median.
* Qualifying: 54,269 stored telemetry samples, 7 proper laps, 6 excluded laps; proper-lap rates ranged from about 26.5 Hz to 30.0 Hz, with a 28.0 Hz median.

Those fixtures remain useful regression data for collector design, but they are not the final source-quality verdict.

The later telemetry-only diagnostic in `tools/rf2-poc/diagnostic-raw/` isolated the source cadence more cleanly:

* target polling rate: `100 Hz`;
* captured duration: about `600 s`;
* stored telemetry frames: `29,502`, or `147,510` vehicle samples across 5 cars;
* stored raw-frame rate: about `49.17 Hz`;
* telemetry update-counter rate: about `49.97 Hz`;
* update-counter gaps: `191` events, `478` inferred missed source updates, about `1.6%` of source updates over the run;
* raw-frame analysis saw no torn frames written to storage.

Conclusion: preservation quality is good enough to use server-published telemetry as the default capture source. The result is near-complete source preservation, not literal 100% capture of every source update. Weak channel-change rates, especially brake, are secondary evidence and can simply reflect steady driver inputs.

---

## Reproduce The PoC

Required runtime:

* Windows.
* 64-bit Python 3.11+; the development machine was validated with Python 3.14.
* No `pip install` step is currently required.
* A browser on the same machine, or LAN access to the selected HTTP port.

Reference procedure followed during the PoC:

* verify Python on the host and run from the repository root;
* prove the browser/API path first with fixture mode;
* validate the shared-memory reader with the repo tests;
* run against live shared memory from `rFactor2.exe` or `Dedicated.exe`;
* inspect visible maps and analyze recorded captures when live decoding or cadence looked suspicious.

Reference commands:

```powershell
python tools/rf2-poc/run_poc.py --source mock
python tools/rf2-poc/run_poc.py --source shared-memory
Get-CimInstance Win32_Process | Where-Object { $_.Name -like '*Dedicated*.exe' } | Select-Object ProcessId, Name, ExecutablePath, CommandLine
python tools/rf2-poc/run_poc.py --source shared-memory --pid <PID>
python tools/rf2-poc/run_poc.py --source shared-memory --map-name 'Global\$rFactor2SMMP_Scoring$12345' --telemetry-map-name 'Global\$rFactor2SMMP_Telemetry$12345'
python tools/rf2-poc/list_memory_maps.py --pid <PID>
python tools/rf2-poc/analyze_recordings.py tools/rf2-poc/diagnostic-raw
python -m compileall -q tools/rf2-poc
python -m unittest discover tools/rf2-poc/tests
```

If LAN viewing is needed, bind to `0.0.0.0` and open `http://<host-pc-ip>:8877/poc` from another machine.

---

## Concise Troubleshooting

If a live map cannot be opened:

* Confirm `Dedicated.exe` or `rFactor2.exe` is running.
* Confirm the current dedicated-server PID; it changes on restart.
* Confirm the shared-memory plugin is installed, enabled, and using the expected `CustomPluginVariables.json`.
* Confirm scoring/telemetry buffers are not disabled by `UnsubscribedBuffersMask`.
* Confirm the PoC process can see the map namespace: same user/session for non-global maps, or `Global\...` with `Create Global Objects` permission.
* Run `list_memory_maps.py --pid <PID>` before changing code.

If the browser shows impossible values, such as huge vehicle counts or nonsensical session codes, the map exists but the payload layout or decode offset may be wrong. Record the map name, source process, plugin version, decode offset, and `/api/snapshot` JSON before changing parser structs.

If the browser shows fixture data, check the source/status line. `mock` or `shared memory unavailable; using fixture replay` is not live proof.

If the browser cannot connect, confirm the PoC process is still running, the URL uses the selected port, and firewall rules allow the port when viewing from another machine.

If dedicated-server live mode still fails while client mode works, check only these points before changing code:

* the dedicated-server profile's `CustomPluginVariables.json` actually enables the plugin;
* `Dedicated.exe` was restarted after the config change;
* the server session is active enough to emit scoring;
* the map is visible in the expected namespace;
* exact PID-suffixed map names were tried when automatic probes were insufficient.

For deeper plugin startup, namespace, and logging analysis, use `docs/shared-memory-plugin-investigation.md` rather than expanding this PoC note again.
