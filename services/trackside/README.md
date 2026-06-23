# services/trackside

Phase 0B production scaffold for Trackside.

This solution is intentionally small but shaped like the final application: one Windows-oriented host process owns local HTTP hosting, source abstraction, SignalR push plumbing, tray integration, and static kiosk hosting.

## Projects

- `src/Trackside.Domain` - pure live-session domain records.
- `src/Trackside.Application` - source contracts, application options, JSON settings, and shared live-session state.
- `src/Trackside.Infrastructure` - fixture adapter and future rFactor 2/shared-memory/SQLite implementations.
- `src/Trackside.Host` - executable ASP.NET Core host targeting `.NET 10` and `net10.0-windows`.
- `src/Trackside.RigAgent` - idle worker scaffold for future rig-side telemetry/setup/spectator support.
- `tests/Trackside.Tests` - xUnit tests for fixture contracts, API route stability, CLI aliases, and shared-memory parser scaffolding.

## Architecture Choices

- ASP.NET Core Generic Host provides dependency injection, configuration, logging, hosted services, and graceful shutdown.
- The solution uses a light Clean Architecture / Ports and Adapters split: Host composes, Application defines ports, Infrastructure implements adapters, Domain stays pure.
- SignalR is the browser push layer. Clients should load `/api/live-session/current` first, then subscribe to `/hubs/live-session`.
- `ILiveSessionSource` hides whether the current snapshot comes from a fixture, recorded data, or future shared-memory parsing.
- The tray shell uses Windows Forms `NotifyIcon` because it is the standard Windows notification-area API and keeps tray behavior in the same executable.
- A separate `Trackside.RigAgent` binary exists so future client/rig-side behavior does not get mixed into the central host or browser UI.
- Phase 0B deliberately does not read rFactor 2 memory maps yet. `MappedBufferPayloadLocator` and `IRf2ScoringPayloadParser` establish the parser seam and tests.

## Commands

Run from the repository root:

```powershell
dotnet build services\trackside\Trackside.slnx
dotnet test services\trackside\Trackside.slnx
dotnet run --project services\trackside\src\Trackside.Host -- --source fixture --fixture Fixtures\mock-live-session.json --no-tray
```

Open `http://127.0.0.1:8877` for the packaged/static kiosk shell.

Tray mode is enabled by default on Windows. Disable it in development with `--no-tray` when you want the process to behave like a normal console web app.

## Configuration

The `Trackside` section in `src/Trackside.Host/appsettings.json` controls:

- `Http.ListenUrl` - Kestrel binding URL.
- `Http.PublicBaseUrl` - URL opened by tray actions.
- `Source.Mode` - currently `Fixture`; future modes are `SharedMemory` and `Recorded`.
- `Source.FixturePath` - normalized live-session fixture JSON.
- `LiveSession.PublishIntervalSeconds` - background SignalR publish cadence.
- `Tray.MenuItems` - configurable clickable tray menu entries.

Tray menu actions support:

- `OpenUrl` with either `Url` or app-relative `Route`.
- `Separator`.
- `Exit` for graceful host shutdown.

## Extension Points

- Add shared-memory parsing behind `IRf2ScoringPayloadParser` and `ILiveSessionSource` without changing API or kiosk contracts.
- Add storage as a separate service behind repositories/workers; do not put SQLite writes in the source reader hot path.
- Add admin controls as new endpoint groups and React routes while keeping `/api/live-session/current` stable for kiosk reconnects.
