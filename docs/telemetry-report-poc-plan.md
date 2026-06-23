# Telemetry Report PoC Decision

This note records the completed telemetry report proof of concept and the implementation constraints it leaves behind.

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

In shared-memory mode, `--telemetry-collector auto` now starts a dedicated telemetry-only Python process. That worker reads only the telemetry map and writes compact raw frames, while the main PoC process polls scoring/session metadata at `--scoring-record-hz` (default `5.0`). Use `--telemetry-collector in-process` to compare against the older combined collector path.

Caveat: lap distance / lap percent currently come from the scoring row, while throttle/brake/steer/gear/G/speed come from telemetry. If scoring updates slower than telemetry, the high-rate samples can contain repeated lap-percent values with changing telemetry values. That is still useful for this proof, but final report quality should be validated live.

Earlier Bahrain practice/qualifying captures in `services/leaderboard/poc/tests/data/` showed that the older combined Python collector did not consistently record effective 50 Hz per-driver telemetry. The qualifying proper laps were around 28-30 Hz, while the practice proper laps were much lower and uneven. Later telemetry-only diagnostics isolated that as a collector-design issue rather than proof that the dedicated-server shared-memory source is insufficient.

## Runtime files

The PoC writes runtime report data under:

```text
services/leaderboard/poc/telemetry-recordings/
```

This folder is gitignored. Each session gets:

* `telemetry_samples.jsonl`: raw observed samples, one JSON object per driver sample;
* `telemetry_raw.jsonl`: compact raw telemetry-map frames imported from the dedicated telemetry worker when process collection is enabled;
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
* `engine_rpm`;
* `throttle_percent`;
* `brake_percent`;
* `gear`;
* `steering_percent`;
* `lateral_g`;
* `longitudinal_g`;
* `vertical_g`;
* `g_magnitude`.

## Report Implementation Findings

Carry these PoC findings into the implementation:

* Record telemetry independently from browser polling.
* In shared-memory mode, capture telemetry in a dedicated process or high-priority loop while scoring/session metadata runs at a lower rate.
* Write compact raw frames first, then expand/report outside the hot read loop.
* Keep full-resolution recorded telemetry samples. Do not resample stored data down to a graph-friendly axis.
* Classify laps as proper, partial, outlap, inlap, or formation/non-timed; only proper laps should drive fastest/reference report decisions.
* Keep diagnostic access to excluded laps for investigation.
* Let report pages compare laps across drivers for speed, throttle, brake, steering, gear, and G-force channels.
* If processing larger sessions becomes slow, add background/indexed report preparation rather than reducing stored telemetry resolution.

## Final Telemetry PoC Decision (Completed)

The telemetry PoC decision is now complete.

### Chosen default architecture

* Use dedicated-server published shared-memory telemetry as the default source.
* Run our telemetry collector read loop at `100 Hz` even though source telemetry is roughly `50 Hz`.
* Run telemetry capture as a dedicated high-priority loop or process for maximum reliability.
* Keep scoring/session reads on a lower-rate path and keep write/report processing off the hot telemetry loop.

Why `100 Hz` polling on a `~50 Hz` source:

* It significantly reduces the chance of missing source updates due to scheduling jitter, serialization stalls, or short write delays.
* Current diagnostic evidence in `docs/core-poc.md` shows near-complete source preservation at this strategy, with only a small source-to-storage cadence gap.

### Interpreting quality metrics

Preservation quality is the primary pass/fail signal for this phase.

* Primary: source update-counter cadence vs stored frame cadence, gap counts, long interval outliers, torn reads.
* Secondary: per-channel change-rate metrics (for example brake/throttle change frequency), which can be low even when capture fidelity is healthy because driver inputs can be steady for long periods.

Channel-quality "fails" should not override a preservation "pass" unless they correlate with explicit source-preservation problems.

### Mandatory modular telemetry-source design

Implementation must keep telemetry input modular so report generation and driver analytics are source-agnostic.

Required design rule:

* downstream telemetry consumers (report builder, lap analyzer, statistics jobs) must depend on a normalized telemetry ingestion interface, not on a specific collector origin.

Supported source modes:

* default mode: central server collector reads dedicated-server maps;
* optional mode: rig-local collector services read setup-local maps and upload session telemetry to server.

The venue owner should be able to choose the mode via configuration, without code changes to report/analysis modules.

### Rig-local mode kept as a first-class option

The implementation must keep the door open for direct setup telemetry capture if venue priorities change later.

Target operational model for optional rig-local mode:

* server issues session-level control intent (for example start/stop recording telemetry);
* a tiny local service on each setup records telemetry locally during the session;
* local service uploads session telemetry to the central server after session finalization;
* central server continues to own scoring/session truth and final report generation.

This preserves today's simpler default deployment while enabling a later shift to setup-local capture with minimal architecture churn.

## Open implementation questions for later phases

* Should the final app store every telemetry sample, or only best-lap candidates plus summaries?
* Should report pages compare every driver to the fastest session lap, the same driver’s previous best, or both?
* Do staff want reports generated only for counted sessions, or every completed session?
* What print format should be used later: browser print, generated PDF, or both?
