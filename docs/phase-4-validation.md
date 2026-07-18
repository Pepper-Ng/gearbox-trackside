# Phase 4 rFactor 2 Integration and Venue Validation Record

**Record date:** 2026-07-18  
**Validation status:** Complete for the agreed Phase 4 live-integration and venue-readiness scope  
**Evidence owner:** Operator-reported venue run, supported by the existing local implementation and automated tests

This record documents the actual venue findings and the decisions made about what was and was not tested. It is intentionally not a claim that venue data can be exported or retained: the venue's data-integrity and security rules prohibited extracting real-world samples.

## 1. Conclusion

Phase 4 can be signed off for the validated scope. Trackside has been exercised against real venue operation and the equivalent local rFactor 2 Dedicated Server setup. The local server behaves the same as the venue server, so subsequent investigation and hardening can be performed locally without repeatedly taking the application to the venue.

The validated venue-ready scope is:

- the service runs reliably in `--console` mode;
- live scoring reaches the service and the live timing display;
- five simulator setups can run simultaneously with real scoring data;
- dedicated-server restart and new-PID map rediscovery work without Trackside reconfiguration;
- Trackside restart, including while rFactor 2 remains running, reconnects to the active maps;
- browser/SignalR reconnect recovers the live display;
- track changes are handled;
- driver IDs and profile associations remain usable for the venue workflow.

The Phase 4 close-out UI refinements are listed in [phase-4-follow-up-todo.md](./phase-4-follow-up-todo.md). They should be completed before starting Phase 5 so the operator-facing surface is not carried forward with known usability issues. Track-outline generation and captured real-data regression fixtures are explicit non-blocking deferrals, not failed validation items.

## 2. Test environment and identity

| Item | Recorded value | Confidence / limitation |
| --- | --- | --- |
| Venue topology | One rFactor 2 Dedicated Server and five simulator clients on the venue LAN | Verified during the venue run |
| Equivalent local topology | Local Dedicated Server and client setup with the same operational behavior as the venue setup | Verified by the operator; this is the approved environment for later analysis |
| Trackside run mode | `Trackside.Service --console` | Verified; Windows Service installation and reboot recovery were not part of this sign-off |
| rFactor 2 Dedicated Server build | **Not recorded** | The exact executable/Steam build number was not captured and must not be inferred from this record |
| rFactor 2 client build | **Not recorded** | The exact client build number was not captured; the local and venue setups were observed to behave equivalently |
| Shared-memory plugin | rF2 Shared Memory Map Plugin **3.7.15.1**, x64 | The vendored and upstream-referenced DLL identity is documented in [vendor/README.md](../vendor/README.md) and [rfactor2-dedicated-crash-runbook.md](./rfactor2-dedicated-crash-runbook.md) |
| Server content | F1 DLC cars/tracks and the venue's representative server content | Exact event, car, and track package names were not retained |
| Client content | The five licensed venue client installations with the corresponding F1 DLC content | Exact package/profile inventory was not retained |
| Server profile | Active profile path/name not recorded | The runbook requires the active `+profile` value to be captured during a future evidence run |
| Client profiles | Fixed rig identities such as `Setup1`, `Setup2`, and the other setup names | Exact profile paths were not retained; driver-ID/profile association was verified at the Trackside workflow level |

The missing build/profile metadata is a documentation gap, not evidence of a runtime incompatibility. If exact build provenance becomes operationally important, capture it during the next local maintenance window without reopening the venue validation.

## 3. Shared-memory maps, PID, and namespace

Trackside supports the following plugin map families:

| Data | Base/client map | Dedicated-server candidate |
| --- | --- | --- |
| Scoring | `$rFactor2SMMP_Scoring$` | `$rFactor2SMMP_Scoring$<PID>` or `Global\$rFactor2SMMP_Scoring$<PID>` |
| Telemetry | `$rFactor2SMMP_Telemetry$` | `$rFactor2SMMP_Telemetry$<PID>` or `Global\$rFactor2SMMP_Telemetry$<PID>` |

The venue run confirmed that PID autodiscovery works and that a restarted Dedicated Server is followed through its new PID. The exact PID, successful map spelling, and successful namespace for that run were not retained in an evidence artifact. Therefore this record deliberately records the map families and the discovery behavior, rather than inventing a PID or claiming that the local-session or `Global\` candidate was the one that won the probe.

The implementation probes both session-local and `Global\` candidates and can also enumerate visible Windows `Section` objects. It opens maps read-only. The current Phase 4 venue sign-off is for the scoring/live-timing path; telemetry map capture and high-cadence telemetry quality remain Phase 5 work.

## 4. Fields confirmed by the live path

The following fields were confirmed at the live scoring/display and venue-workflow level. No raw venue payload was retained.

### Session and environment

- track name and track changes;
- session type and current session state;
- session timing/lap-distance context where exposed;
- ambient and track temperatures;
- overall flag/yellow state;
- vehicle count and live session presence.

### Driver and scoring rows

- rFactor 2 driver/rig name;
- rFactor 2 scoring driver ID;
- driver-ID stability for a rig within a session;
- Trackside driver profile association;
- vehicle/content name;
- race position and ordering;
- completed-lap count;
- valid-lap value (`0`, `1`, or `2`) and the Phase 2 rule that only `2` is eligible for a valid timed board result;
- best, last, and current lap timing;
- current sector and best/last sector timing;
- gap-to-leader/next driver where exposed;
- lap progress and track position where exposed;
- live timing updates for five simultaneous setups.

The venue evidence confirms the operational flow rather than every byte-level field value. Exact field-value regression is intentionally deferred because extracting venue samples was prohibited.

## 5. Validation matrix

| Validation item | Result | Notes |
| --- | --- | --- |
| Dedicated Server start | **Verified** | Venue service operated successfully; equivalent local server behaves the same |
| Client join | **Verified** | Five simulator setups produced live scoring data simultaneously |
| Practice/qualifying/race transitions | **Not run as a separate formal matrix** | The full scripted matrix was intentionally not repeated; session-type handling already exists in the live source and prior validation |
| Track changes | **Verified** | Live Trackside handling follows the new track; track-outline generation was explicitly skipped |
| Completed laps and valid-lap values | **Verified with evidence limitation** | Scoring and Phase 2 lap rules are active in the live path, but no raw venue sample was retained for replay |
| Driver-ID stability within a session | **Verified** | Driver ID and profile association work for the venue workflow |
| Dedicated Server restart and new-PID autodiscovery | **Verified** | Trackside reconnects to the new map without manual PID configuration |
| Trackside service restart | **Verified** | Works while rFactor 2 continues running and resumes from the active maps |
| Browser/SignalR reconnect | **Verified** | Closing and reopening the browser reconnects and restores the live display |
| Real scoring through the live board | **Verified** | Five setups produced real-world data on the live timing display |
| Real scoring through Phase 2 persistence/correction workflows | **Not separately evidenced with a retained venue artifact** | Real scoring reached the live board, but the venue run could not export the resulting database or raw records. Store/correction behavior remains covered by the local implementation and automated tests; this is a reproducibility gap, not a venue data-source failure |
| Track-outline generation | **Explicitly skipped** | Not tested in this Phase 4 run; carry it as a later tracker validation item |
| Captured real-data regression fixture | **Explicitly skipped** | Venue security rules prohibit extracting the required samples; keep this as optional hardening |

## 6. Known gaps and scope decisions

1. The exact rFactor 2 Dedicated Server and client build numbers were not recorded. Do not claim a build number until it is captured from the executable or Steam installation.
2. Exact event/content package names, active profile paths, run PID, and the successful map namespace were not retained. The map autodiscovery behavior itself was verified.
3. The full practice/qualifying/race transition matrix was not executed as one formal scripted Phase 4 exercise. It is intentionally not a blocker for this sign-off.
4. Venue raw scoring payloads and database samples cannot be extracted under the venue's data-integrity/security rules. Local fixtures and automated tests remain the reproducible regression path.
5. Track-outline generation was not tested. It is excluded from Phase 4 completion and should be revisited with the Tracker UI work.
6. This sign-off covers the console-hosted service path. Installed Windows Service behavior, reboot recovery, packaging, rollback, and owner handover belong to later operational/deployment phases.
7. The .NET parser tests currently protect payload/update-counter guards; they do not contain a captured real rFactor 2 payload. Adding a sanitized local scoring fixture remains useful hardening, but is not required for this Phase 4 venue conclusion.
8. The operator-facing UI refinements in [phase-4-follow-up-todo.md](./phase-4-follow-up-todo.md) remain before Phase 5. They are usability close-out work, not evidence of a venue data-source failure.

## 7. Readiness decision

**Decision: proceed from Phase 4 validation to the next implementation phase after the listed UI close-out items are handled.**

For the agreed live-timing and scoring scope, the local and venue environments are sufficiently equivalent and the venue-specific integration risks tested here are resolved. No further venue visit is required to investigate the already-verified restart, autodiscovery, reconnect, track-change, driver-association, or five-setup scoring paths. Future work can use the local server for detailed analysis, regression capture, telemetry work, and tracker development.
