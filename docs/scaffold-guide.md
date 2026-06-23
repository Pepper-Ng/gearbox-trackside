# Trackside Phase 0B Scaffold Guide

This guide explains how the Phase 0B scaffold is meant to be used and extended. It complements the implementation plan and the per-project READMEs; it does not replace them.

The scaffold is a foundation, not the finished leaderboard. It proves the application shape: a Windows-friendly host process, a fixture-backed live-session API, SignalR browser updates, configurable tray actions, and a basic kiosk shell.

## Application Shape

Trackside is currently structured as one local application with two main surfaces:

```text
Trackside.Service (.NET / ASP.NET Core, executable)
  owns service/console startup, local HTTP hosting, API/Hubs, workers, static packaged web files

Trackside.Tray (WinForms executable)
  owns notification-area status/menu, opens service-hosted pages, later may call control endpoints

web/kiosk (React / Vite development app)
  owns browser UI components, typed API client, SignalR client wiring, kiosk layout

Trackside.RigAgent (.NET worker executable, idle scaffold)
  reserved for future rig-side telemetry, setup-name, health, or spectator-mode commands
```

The architecture is not strict MVVM. It uses a light Clean Architecture / Ports and Adapters layout plus a React client:

- Domain model: plain C# records under `services/trackside/Trackside.Domain`.
- Application contracts/state: source interfaces, options, serialization, and state cache under `services/trackside/Trackside.Application`.
- Infrastructure adapters: fixture and future rFactor 2 shared-memory implementations under `services/trackside/Trackside.Infrastructure`.
- Service/transport: HTTP endpoints, SignalR hub contracts, hosted workers, and composition under `services/trackside/Trackside.Service`.
- Tray companion: notification-area menu/status under `services/trackside/Trackside.Tray`.
- Rig agent: future rig-side service binary under `services/trackside/Trackside.RigAgent`.
- Browser UI: React components under `web/kiosk/src/ui`.

Project dependencies point inward: `Service` composes `Infrastructure`, `Application`, and `Domain`; `Infrastructure` implements `Application` contracts and depends on `Domain`; `Application` depends only on `Domain`; `Domain` depends on nothing in Trackside. `Tray` stays a thin companion and talks to the running service through URLs or future control contracts.

Keep these responsibilities separate. Domain objects should not know about HTTP, SignalR, WinForms, rFactor 2 memory maps, or React.

## Important Files

- `global.json` pins the .NET SDK family used by the solution.
- `services/trackside/Trackside.slnx` is the Visual Studio 2026 solution.
- `services/trackside/Directory.Build.props` keeps common C# language/analyzer settings together.
- `services/trackside/Trackside.Domain` contains normalized live-session model records.
- `services/trackside/Trackside.Application` contains source contracts, app options, JSON settings, and shared state.
- `services/trackside/Trackside.Infrastructure` contains fixture and future rFactor 2 adapters.
- `services/trackside/Trackside.Service/Program.cs` is the service executable entry point.
- `services/trackside/Trackside.Service/Hosting` composes the ASP.NET Core app and service/console lifetime.
- `services/trackside/Trackside.Tray` contains the interactive tray companion.
- `services/trackside/Trackside.RigAgent` is an idle scaffold for a future service deployed on rigs.
- `services/trackside/Trackside.Service/appsettings.json` configures HTTP, source mode, publish cadence, and CORS.
- `services/trackside/Trackside.Service/Fixtures/mock-live-session.json` is the normalized fixture used before rFactor 2 parsing exists.
- `services/trackside/Trackside.Service/wwwroot` is the minimal static kiosk served by the packaged service.
- `services/trackside/Trackside.Tests` contains solution-level tests.
- `web/kiosk/src/tracksideApi.ts` is the typed browser REST/SignalR client.
- `web/kiosk/src/ui/App.tsx` is the current basic kiosk layout.

## Runtime Behavior

`Trackside.Service` starts Kestrel on `http://127.0.0.1:8877` by default. It reads the configured live-session source, caches the latest snapshot, exposes REST endpoints, and broadcasts snapshots through SignalR.

Current endpoints:

- `/` serves the packaged/static kiosk page.
- `/api/live-session/current` returns the current normalized snapshot.
- `/hubs/live-session` pushes snapshot updates through SignalR.
- `/api/configuration/client` tells browser clients which paths to use.
- `/api/health` reports host/source/tray status.

`Trackside.Tray` is a separate companion process. The tray icon does not contain the application UI itself; it is a small control/status surface. Its menu items open service-hosted webpages. Configure those items in `TracksideTray:MenuItems` in the tray app settings.

For venue rollout, the preferred runtime shape is Windows services plus browser pages, with a tray companion for interactive venue users. The tray companion should make it obvious after reboot that Trackside is running and should reopen dashboards quickly. Collection/scoring/storage should not depend on an interactive desktop session.

## How To Run

From the repository root:

```powershell
dotnet build services\trackside\Trackside.slnx
dotnet test services\trackside\Trackside.slnx
dotnet run --project services\trackside\Trackside.Service -- --console --source fixture --fixture Fixtures\mock-live-session.json
```

Open the hosted kiosk at:

```text
http://127.0.0.1:8877
```

To run the tray companion, start it separately while the service is running:

```powershell
dotnet run --project services\trackside\Trackside.Tray
```

The tray icon appears in the Windows notification area. Right-click it to open configured options such as `Open Kiosk`, `Open Health`, or `Exit Tray`. Double-clicking the icon opens the kiosk.

For frontend development:

```powershell
npm --prefix web\kiosk install
npm --prefix web\kiosk run dev
```

The Vite dev server proxies `/api` and `/hubs` to the backend, so keep `Trackside.Service` running at the same time.

The rig-agent scaffold builds as part of the same solution. It is intentionally idle in Phase 0B:

```powershell
dotnet run --project services\trackside\Trackside.RigAgent
```

## Deployment Direction

Early venue builds should be versioned file bundles copied or extracted into a known install folder. Add service install/update scripts before venue rollout. A full installer can come later if service setup, shortcuts, firewall rules, or rollback become too awkward for scripts.

Remote updates should be a later dashboard-controlled feature: check a signed/versioned manifest, show that an update is available, download a bundle, verify signature/checksum, stop services, swap files with rollback, and restart. Do not silently auto-update during active sessions.

## How To Extend

Add new backend behavior in the narrowest layer that owns it:

- New source type: add the contract to `Trackside.Application` if needed, implement the adapter in `Trackside.Infrastructure`, then register it in `Trackside.Service` composition.
- Shared-memory parsing: implement `IRf2ScoringPayloadParser` under `Trackside.Infrastructure/Rf2/SharedMemory`. Keep map opening and byte parsing away from API endpoints and UI code.
- API endpoint: add a focused endpoint group under `Api`; keep stable paths in route constants when browser code depends on them.
- SignalR message: extend `ILiveSessionClient` only when push behavior genuinely needs a new browser message.
- Background work: use `BackgroundService` or hosted services, and keep slow persistence/reporting out of source read loops.
- Tray option: add a configuration-driven action in `Tray`; avoid hard-coding venue-specific menu items.
- Kiosk UI: add React components under `web/kiosk/src/ui`; keep backend calls in `tracksideApi.ts` or small client modules.

The browser should load `/api/live-session/current` first, then subscribe to `/hubs/live-session`. That keeps reconnects simple and avoids making a page refresh part of normal operation.

## Current Limits

The scaffold intentionally does not yet include:

- rFactor 2 memory-map reading;
- real leaderboard sorting/highlighting logic beyond fixture display;
- SQLite persistence;
- staff alias/admin flows;
- packaging/service installation;
- remote update installation;
- camera, telemetry reports, PDF, or printing.

Those features should be added on top of the existing seams rather than by replacing the host shape.
