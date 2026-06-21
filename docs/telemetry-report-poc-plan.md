# Telemetry Report PoC Tracker

This note tracks the report/printing proof of concept that builds on the expanded shared-memory dashboard.

## What the live dashboard result means

The current live result is strong evidence that the dedicated-server shared-memory path exposes the project-critical live fields:

* scoring rows for all current vehicles;
* all-car telemetry rows joined to scoring rows by vehicle ID;
* throttle, brake, steering, gear, speed, and local acceleration/G-force values updating live;
* lap distance, track-percent, sector timing, lap timing, position, gaps, and finish status from scoring.

This is enough to proceed with the leaderboard/data-source direction, with one important distinction: shared memory is a live snapshot, not a completed-session archive. Historical lap/sector/telemetry reports still need a service to record values as they appear.

## G-force interpretation

The telemetry map exposes `mLocalAccel`, acceleration in local vehicle coordinates. The PoC records and reports these as:

* lateral G: local `x` axis;
* vertical G: local `y` axis;
* longitudinal G: local `z` axis;
* magnitude: combined vector magnitude.

The signs should be validated against a known braking/acceleration/cornering run before final UI wording is locked. For the PoC, all axes are preserved so no information is lost.

## Browser update rate vs telemetry rate

`--poll-seconds` controls only how often the browser asks `/api/snapshot` to redraw. It is not the telemetry map update rate.

The shared-memory plugin telemetry path is designed around about 50 Hz frames. The PoC now has a background sampler controlled by `--telemetry-record-hz` and defaults to `50.0`, so recording can run faster than the browser refresh.

Caveat: lap distance / lap percent currently come from the scoring row, while throttle/brake/steer/gear/G/speed come from telemetry. If scoring updates slower than telemetry, the high-rate samples can contain repeated lap-percent values with changing telemetry values. That is still useful for this proof, but final report quality should be validated live.

The real Bahrain practice/qualifying captures in `services/leaderboard/poc/tests/data/` show that the current Python server-side PoC did not consistently record effective 50 Hz per-driver telemetry. The qualifying proper laps are around 28-30 Hz, while the practice proper laps are much lower and uneven. Treat this as a collector implementation finding, not yet as proof that the dedicated-server shared-memory source is insufficient. The next collector iteration should measure and optimize telemetry read-loop timing before deciding to distribute collectors to each rig.

## Runtime files

The PoC writes runtime report data under:

```text
services/leaderboard/poc/telemetry-recordings/
```

This folder is gitignored. Each session gets:

* `telemetry_samples.jsonl`: raw observed samples, one JSON object per driver sample;
* `report.json`: finalized telemetry report data when the session finalizes.

The PoC writes rotating runtime logs under:

```text
services/leaderboard/poc/logs/
```

The logs include session start/finalize events, sample-file write failures, report-build failures, report-build timing, and report API requests. Routine successful dashboard polling is kept out of the normal log output.

Each telemetry sample stores at least:

* `lap_distance`;
* `lap_percent`;
* `speed_kph`;
* `throttle_percent`;
* `brake_percent`;
* `gear`;
* `steering_percent`;
* `lateral_g`;
* `longitudinal_g`;
* `vertical_g`;
* `g_magnitude`.

## Implemented PoC report behavior

* The recorder samples independently from browser polling.
* Completed sessions in `/history` link to `/telemetry?session=<session-id>` when there is reportable telemetry.
* `/telemetry` lists stored recordings from `services/leaderboard/poc/telemetry-recordings/`, can load old reports after a collector restart, and can open local `report.json` or `telemetry_samples.jsonl` files.
* The report classifies laps as proper, partial, outlap, inlap, or formation/non-timed.
* Only proper laps are considered for fastest personal/overall report references.
* The viewer can still select and compare any stored lap, including excluded partial/out/in/formation laps, for diagnosis.
* The report stores and displays full-resolution recorded telemetry samples. It does not resample reports down to a reduced graph axis.
* Each graph can compare two selected laps from the same or different drivers for speed, throttle, brake, steering, gear, lateral G, longitudinal G, and vertical G.
* Fastest personal and fastest overall proper laps are marked in the lap selectors.
* The X axis can use time, track percent, or raw sample index.
* Hovering a graph shows the nearest selected samples and values for the compared laps.
* Report JSON generation starts on a background thread when a session finalizes. If the page is opened early, `/api/reports/<session-id>` can return `building` and the page polls until the report is ready.

The first versions used resampled graph axes, first one point per percent and later a capped adaptive track-percent axis. The current version keeps every recorded sample in the report so telemetry can be reviewed at captured resolution. If processing larger sessions becomes slow, preserve the full raw sample files and add background/indexed report preparation rather than reducing stored telemetry resolution.

## Final Telemetry PoC Decision Plan

The next PoC work should answer one decision: can the venue use one central collector on or near the dedicated server for both scoring and all-car telemetry, or does high-fidelity telemetry require rig-local collectors?

The current evidence is not enough to decide that. It shows that the current Python server-side PoC did not consistently capture 50 Hz per-driver telemetry, but it does not prove whether the bottleneck is the Python loop, file/JSON work, the dedicated-server shared-memory source, server load, or rFactor 2 network/client update behavior.

### Step 1 - Add repeatable analysis tooling

Agent implementation task:

* Add a small command such as `services/leaderboard/poc/analyze_recordings.py` that reads one or more `telemetry-recordings/<session-id>/` folders or `tests/data/` fixtures.
* Report, per session and per proper lap: sample count, lap time, effective samples/second, min/median/p95/max sample interval, repeated `lap_percent` count, telemetry update-counter gaps/repeats, gear-zero share, and channel change fractions for throttle, brake, steering, gear, speed, and lap percent.
* Output both console text and machine-readable JSON so the numbers can be copied into docs and compared across runs.
* Add tests using the existing Bahrain fixtures in `services/leaderboard/poc/tests/data/`.

Verification:

```powershell
python services/leaderboard/poc/analyze_recordings.py services/leaderboard/poc/tests/data
python -m unittest discover services/leaderboard/poc/tests
```

Expected result: the analyzer reproduces the known finding that the qualifying capture is roughly 28-30 Hz on proper laps while the practice capture is lower and uneven.

### Step 2 - Baseline the current central server collector

Simple agent procedure on the actual system:

1. Start the dedicated-server

   ```powershell
  Start-Process -FilePath "H:\Games\rFactor2\rFactor2 Dedicated.exe" -WorkingDirectory "H:\Games\rFactor2" -ArgumentList "+oneclick"
   ```

2. Wait approximately 1 minute for the process to start, then record the dedicated-server PID:

   ```powershell
   Get-CimInstance Win32_Process | Where-Object { $_.Name -like '*Dedicated*.exe' } | Select-Object ProcessId, Name, ExecutablePath, CommandLine
   ```

3. Start the current PoC collector:

   ```powershell
   python services/leaderboard/poc/run_poc.py --source shared-memory --pid <PID> --telemetry-record-hz 50 --poll-seconds 1
   ```

4. Run at least three controlled sessions:
   * one practice session with all cars running at least five timed laps;
   * one qualifying-style session with all cars running at least five timed laps;
   * one race or session-ending flow so finalization and report generation are exercised.
   (These sessions will automatically start after starting the decidated server (Step 1), Practice takes 20 minutes, Qualification 10 minutes, and the race 5 laps.)
5. During the run, open `/poc` and confirm telemetry is connected and all scored vehicles are joined.
6. After each session finalizes, open `/telemetry?session=<session-id>` and confirm proper laps are classified and graphs render.
7. Run the analyzer over the created recording folders.
8. Save `trackside-poc.log`, the analyzer JSON/text output, and the generated recording folders as evidence.

Decision check:

* If proper laps are consistently at or above about 45 Hz per active driver, with no unexplained long gaps and acceptable channel quality, the central collector path is good enough for final implementation.
* If proper laps are far below 45 Hz, continue to Step 3 before changing architecture.

### Step 3 - Isolate source cadence from collector overhead

Agent implementation task:

* Add a telemetry-only diagnostic mode or command that reads only the telemetry map for a short fixed window, without scoring joins, report generation, HTTP rendering, or per-sample JSONL writes in the hot path.
* Keep samples in memory or write batched chunks after capture.
* Log the telemetry map update counter, read-loop timing, per-driver sample counts, and late/dropped-loop counts.
* Run scoring reads separately at a lower rate only if needed for driver names.

Simple agent procedure on the actual system:

1. With the same dedicated-server session running, execute the telemetry-only diagnostic for 2-5 minutes.
2. Repeat with 1 car, then 3 cars, then 6 cars if possible.
3. Run the analyzer on the diagnostic output.
4. Compare telemetry-only rates to the full PoC collector rates from Step 2.

Decision check:

* If telemetry-only capture reaches about 50 Hz but the full PoC does not, the bottleneck is collector design: Python loop structure, scoring joins, JSON serialization, file I/O, or report work. The final implementation should use a decoupled collector with queues/batching, likely in .NET/C# for the Windows service.
* If telemetry-only capture is still around 28-30 Hz or lower, the bottleneck is probably upstream of the Python report pipeline: dedicated-server telemetry map update cadence, rFactor 2 server/client telemetry availability, plugin behavior in `Dedicated.exe`, or server load.

### Step 4 - Optimize the central collector if the source is good enough

Only do this if Step 3 shows the source can provide near-50 Hz but the full PoC collector cannot.

Agent implementation task:

* Split the collector into separate loops:
  * telemetry map reader at 50 Hz or faster;
  * scoring/session reader at 5-10 Hz;
  * asynchronous/batched writer;
  * report builder outside the hot path.
* Add timing metrics to the logs and analyzer output.
* Keep raw sample storage at captured resolution; do not reduce telemetry to make reports easier.

Decision check:

* If the optimized central collector sustains about 45-50 Hz per active driver with six cars, proceed to final implementation with a central collector architecture.
* If not, continue to Step 5.

### Step 5 - Run a one-rig local collector fallback spike

Only do this after Steps 2-4 show central collection cannot meet the report-quality target.

Operator/venue prerequisite:

* Install or enable `rF2SharedMemoryMapPlugin` on one non-critical simulator rig.
* Run a collector on that rig that records only that rig's own telemetry.
* Keep the central server collector running for scoring/session/lap timing.

Simple agent procedure on the actual system:

1. Run the same controlled sessions as Step 2.
2. Record central server scoring/telemetry and rig-local own-car telemetry at the same time.
3. Use wall-clock timestamps, driver name/vehicle ID, lap number, and lap times to align the rig-local telemetry with central scoring.
4. Analyze rig-local own-car sample rate and channel quality.
5. Compare rig-local telemetry against central telemetry for the same driver/laps.

Decision check:

* If rig-local telemetry is near 50 Hz and materially smoother while central telemetry is not, document a distributed telemetry architecture for final implementation: central server for scoring/session/leaderboard, rig-local services for own-car high-rate telemetry, and a synchronization/merge step.
* If rig-local telemetry is not materially better, do not add six rig-local services. Keep the final MVP focused on live leaderboard and lower-fidelity telemetry reports, or defer telemetry reports until a better data source is found.

### Final PoC exit criteria

The telemetry PoC is complete when the repo contains:

* recorded evidence from the current central collector;
* telemetry-only source-cadence evidence;
* optimized central collector evidence if needed;
* one-rig local fallback evidence if central collection still fails;
* a written decision: central-only telemetry, central scoring plus rig-local telemetry, or defer high-fidelity telemetry reports.

After that decision is written, stop expanding the PoC and start final implementation against the chosen architecture. The live leaderboard can proceed independently as soon as the scoring/session source is accepted, even if high-fidelity telemetry remains a later or distributed feature.

## Open implementation questions for later phases

* Should the final app store every telemetry sample, or only best-lap candidates plus summaries?
* Should report pages compare every driver to the fastest session lap, the same driver’s previous best, or both?
* Do staff want reports generated only for counted sessions, or every completed session?
* What print format should be used later: browser print, generated PDF, or both?
