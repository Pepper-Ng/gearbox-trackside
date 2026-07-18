# gearbox-trackside

Spectator camera direction, automatic leaderboards, and printable per-driver telemetry reports for Gearbox Race Café's rFactor 2 simulator setup.

The production Trackside service, tray companion, rig agent, and tests live in `services/trackside`. The Phase 0A Python shared-memory proof of concept is preserved under `tools/rf2-poc`, and the vendored rF2 shared-memory plugin snapshot lives under `vendor/rf2-shared-memory-map`.

Current status: Phase 0A through Phase 3 implementation work is complete, and Phase 4 local rFactor 2 integration plus the agreed venue-readiness validation is complete for the live scoring scope. The operator UI close-out items are recorded in `docs/phase-4-follow-up-todo.md` before Phase 5 continues.

The Phase 4 evidence and scope decision are recorded in `docs/phase-4-validation.md`. Earlier proof-of-concept notes live in `docs/core-poc.md`. The Phase 0B scaffold structure and extension guide lives in `docs/scaffold-guide.md`. The Phase 0C deployment skeleton is documented in `docs/deployment-skeleton.md`. The telemetry report PoC decision plan, including central-server versus rig-local collector testing, lives in `docs/telemetry-report-poc-plan.md`.
