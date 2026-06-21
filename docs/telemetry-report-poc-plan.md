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
* The X axis can use track percent or raw sample index.
* Hovering a graph shows the nearest selected samples and values for the compared laps.
* Report JSON generation starts on a background thread when a session finalizes. If the page is opened early, `/api/reports/<session-id>` can return `building` and the page polls until the report is ready.

The first versions used resampled graph axes, first one point per percent and later a capped adaptive track-percent axis. The current version keeps every recorded sample in the report so telemetry can be reviewed at captured resolution. If processing larger sessions becomes slow, preserve the full raw sample files and add background/indexed report preparation rather than reducing stored telemetry resolution.

## Next validation checklist

1. Run the PoC against a live dedicated-server session with `--pid <PID>`.
2. Keep default `--telemetry-record-hz 50` and browser `--poll-seconds 1`.
3. Complete at least one clean timed lap per driver.
4. End the session so game phase reaches session-over, or finish a race so all drivers receive finish status.
5. Open `/history` and follow the telemetry report link.
6. Confirm the report has one best lap per driver and one fastest reference lap.
7. Check whether lap-percent coverage is close to 0-100% for each best lap.
8. Validate G-force axis signs with an obvious acceleration, braking, and cornering section.
9. Decide whether final reports should use scoring lap distance, inferred distance from coordinates, or another source if scoring-rate lap percent is too coarse.

## Open implementation questions for later phases

* Should the final app store every telemetry sample, or only best-lap candidates plus summaries?
* Should report pages compare every driver to the fastest session lap, the same driver’s previous best, or both?
* Do staff want reports generated only for counted sessions, or every completed session?
* What print format should be used later: browser print, generated PDF, or both?
