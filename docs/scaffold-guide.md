# Trackside Phase 0B Scaffold Guide

This guide explains how the Phase 0B scaffold is meant to be used and extended. It complements the implementation plan and the per-project READMEs; it does not replace them.

The scaffold is a foundation, not the finished leaderboard. It proves the application shape: a Windows-friendly host process, a fixture-backed live-session API, SignalR browser updates, configurable tray actions, and a basic kiosk shell.

## Application Shape

Trackside is currently structured as one local application with two main surfaces:

```text
Trackside.Host (.NET / ASP.NET Core, executable)
  owns process startup, local HTTP hosting, API/Hubs, tray menu, static packaged web files

web/kiosk (React / Vite development app)
  owns browser UI components, typed API client, SignalR client wiring, kiosk layout

Trackside.RigAgent (.NET worker executable, idle scaffold)
  reserved for future rig-side telemetry, setup-name, health, or spectator-mode commands
```

The architecture is not strict MVVM. It uses a light Clean Architecture / Ports and Adapters layout plus a React client:

- Domain model: plain C# records under `services/trackside/src/Trackside.Domain`.
- Application contracts/state: source interfaces, options, serialization, and state cache under `services/trackside/src/Trackside.Application`.
- Infrastructure adapters: fixture and future rFactor 2 shared-memory implementations under `services/trackside/src/Trackside.Infrastructure`.
- Host/transport: HTTP endpoints, SignalR hub contracts, hosted workers, composition, and tray integration under `services/trackside/src/Trackside.Host`.
- Rig agent: future rig-side service binary under `services/trackside/src/Trackside.RigAgent`.
- Browser UI: React components under `web/kiosk/src/ui`.

Project dependencies point inward: `Host` composes `Infrastructure`, `Application`, and `Domain`; `Infrastructure` implements `Application` contracts and depends on `Domain`; `Application` depends only on `Domain`; `Domain` depends on nothing in Trackside.

Keep these responsibilities separate. Domain objects should not know about HTTP, SignalR, WinForms, rFactor 2 memory maps, or React.

## Important Files

- `global.json` pins the .NET SDK family used by the solution.
- `services/trackside/Trackside.slnx` is the Visual Studio 2026 solution.
- `services/trackside/Directory.Build.props` keeps common C# language/analyzer settings together.
- `services/trackside/src/Trackside.Domain` contains normalized live-session model records.
- `services/trackside/src/Trackside.Application` contains source contracts, app options, JSON settings, and shared state.
- `services/trackside/src/Trackside.Infrastructure` contains fixture and future rFactor 2 adapters.
- `services/trackside/src/Trackside.Host/Program.cs` is the executable entry point.
- `services/trackside/src/Trackside.Host/Hosting` composes the ASP.NET Core app and process lifetime.
- `services/trackside/src/Trackside.RigAgent` is an idle scaffold for a future service deployed on rigs.
- `services/trackside/src/Trackside.Host/appsettings.json` configures HTTP, source mode, publish cadence, CORS, and tray menu items.
- `services/trackside/src/Trackside.Host/Fixtures/mock-live-session.json` is the normalized fixture used before rFactor 2 parsing exists.
- `services/trackside/src/Trackside.Host/wwwroot` is the minimal static kiosk served by the packaged host.
- `services/trackside/tests/Trackside.Tests` contains solution-level tests.
- `web/kiosk/src/tracksideApi.ts` is the typed browser REST/SignalR client.
- `web/kiosk/src/ui/App.tsx` is the current basic kiosk layout.

## Runtime Behavior

`Trackside.Host` starts Kestrel on `http://127.0.0.1:8877` by default. It reads the configured live-session source, caches the latest snapshot, exposes REST endpoints, and broadcasts snapshots through SignalR.

Current endpoints:

- `/` serves the packaged/static kiosk page.
- `/api/live-session/current` returns the current normalized snapshot.
- `/hubs/live-session` pushes snapshot updates through SignalR.
- `/api/configuration/client` tells browser clients which paths to use.
- `/api/health` reports host/source/tray status.

Tray mode is enabled by default on Windows. The tray icon does not contain the application UI itself; it is a small control surface. Its menu items open hosted webpages or request graceful shutdown. Configure those items in `Trackside:Tray:MenuItems` in `appsettings.json`.

For venue rollout, the preferred runtime shape is Windows services plus browser pages. Tray mode is convenient for development and interactive testing, but collection/scoring/storage should not depend on an interactive desktop session.

## How To Run

From the repository root:

```powershell
dotnet build services\trackside\Trackside.slnx
dotnet test services\trackside\Trackside.slnx
dotnet run --project services\trackside\src\Trackside.Host -- --source fixture --fixture Fixtures\mock-live-session.json --no-tray
```

Open the hosted kiosk at:

```text
http://127.0.0.1:8877
```

To run with the tray icon, omit `--no-tray`:

```powershell
dotnet run --project services\trackside\src\Trackside.Host -- --source fixture --fixture Fixtures\mock-live-session.json
```

The tray icon appears in the Windows notification area. Right-click it to open configured options such as `Open Kiosk`, `Open Health`, or `Exit Trackside`. Double-clicking the icon opens the kiosk.

For frontend development:

```powershell
npm --prefix web\kiosk install
npm --prefix web\kiosk run dev
```

The Vite dev server proxies `/api` and `/hubs` to the backend, so keep `Trackside.Host` running at the same time.

The rig-agent scaffold builds as part of the same solution. It is intentionally idle in Phase 0B:

```powershell
dotnet run --project services\trackside\src\Trackside.RigAgent
```

## Deployment Direction

Early venue builds should be versioned file bundles copied or extracted into a known install folder. Add service install/update scripts before venue rollout. A full installer can come later if service setup, shortcuts, firewall rules, or rollback become too awkward for scripts.

Remote updates should be a later dashboard-controlled feature: check a signed/versioned manifest, show that an update is available, download a bundle, stop services, swap files with rollback, and restart. Do not silently auto-update during active sessions.

## How To Extend

Add new backend behavior in the narrowest layer that owns it:

- New source type: add the contract to `Trackside.Application` if needed, implement the adapter in `Trackside.Infrastructure`, then register it in `Trackside.Host` composition.
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
