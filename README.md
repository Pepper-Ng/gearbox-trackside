# gearbox-trackside

Spectator camera direction, automatic leaderboards, and printable per-driver telemetry reports for Gearbox Race Café's rFactor 2 simulator setup.

The production Trackside service, tray companion, rig agent, and tests live in `services/trackside`. The Phase 0A Python shared-memory proof of concept is preserved under `tools/rf2-poc`, and the vendored rF2 shared-memory plugin snapshot lives under `vendor/rf2-shared-memory-map`.

Current proof-of-concept notes live in `docs/core-poc.md`. The Phase 0B scaffold structure and extension guide lives in `docs/scaffold-guide.md`. The Phase 0C deployment skeleton is documented in `docs/deployment-skeleton.md`. The telemetry report PoC decision plan, including central-server versus rig-local collector testing, lives in `docs/telemetry-report-poc-plan.md`.
