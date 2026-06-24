# services/trackside

Phase 0B production scaffold for Trackside.

This solution is intentionally small but shaped like the final application: one Windows-oriented host process owns local HTTP hosting, source abstraction, SignalR push plumbing, tray integration, and static kiosk hosting.

## Projects

- `Trackside.Domain` - pure live-session domain records.
- `Trackside.Application` - source contracts, application options, JSON settings, and shared live-session state.
- `Trackside.Infrastructure` - fixture adapter and future rFactor 2/shared-memory/SQLite implementations.
- `Trackside.Service` - executable ASP.NET Core service/web runtime targeting `.NET 10` and `net10.0-windows`.
- `Trackside.Tray` - WinForms tray companion that opens service-hosted dashboards/status pages.
- `Trackside.RigAgent` - idle worker scaffold for future rig-side telemetry/setup/spectator support.
- `Trackside.Updater` - tiny out-of-process updater boundary for manifest inspect/verify/plan commands.
- `Trackside.Tests` - xUnit tests for fixture contracts, API route stability, CLI aliases, and shared-memory parser scaffolding.

## Architecture Choices

- ASP.NET Core Generic Host provides dependency injection, configuration, logging, hosted services, and graceful shutdown.
- The solution uses a light Clean Architecture / Ports and Adapters split: Service composes, Application defines ports, Infrastructure implements adapters, Domain stays pure.
- SignalR is the browser push layer. Clients should load `/api/live-session/current` first, then subscribe to `/hubs/live-session`.
- `ILiveSessionSource` hides whether the current snapshot comes from a fixture, recorded data, or future shared-memory parsing.
- The tray companion uses Windows Forms `NotifyIcon` because it is the standard Windows notification-area API, but it is a separate executable from the service.
- A separate `Trackside.RigAgent` binary exists so future client/rig-side behavior does not get mixed into the central host or browser UI.
- A separate `Trackside.Updater` binary exists so future update application does not require the service to overwrite its own running files.
- Phase 0B deliberately does not read rFactor 2 memory maps yet. `MappedBufferPayloadLocator` and `IRf2ScoringPayloadParser` establish the parser seam and tests.

## Commands

Run from the repository root:

```powershell
dotnet build services\trackside\Trackside.slnx
dotnet test services\trackside\Trackside.slnx
dotnet run --project services\trackside\Trackside.Service -- --console --source fixture --fixture Fixtures\mock-live-session.json
```

Open `http://127.0.0.1:8877` for the packaged/static kiosk shell.

Use `--console` for local development. Without it, `Trackside.Service` is configured for Windows Service lifetime when run as an installed service.

Run the tray companion separately when you want the notification-area menu:

```powershell
dotnet run --project services\trackside\Trackside.Tray
```

Create and smoke-test a packaged runtime bundle:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File services\trackside\scripts\New-TracksideBundle.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File <bundle>\install\Invoke-TracksideBundleSmoke.ps1
```

Preview service install/uninstall actions:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File <bundle>\install\Install-Trackside.ps1 -DryRun
pwsh -NoProfile -ExecutionPolicy Bypass -File <bundle>\install\Uninstall-Trackside.ps1 -DryRun
```

## Configuration

The `Trackside` section in `Trackside.Service/appsettings.json` controls:

- `Http.ListenUrl` - Kestrel binding URL.
- `Http.PublicBaseUrl` - URL opened by tray actions.
- `Source.Mode` - currently `Fixture`; future modes are `SharedMemory` and `Recorded`.
- `Source.FixturePath` - normalized live-session fixture JSON.
- `LiveSession.PublishIntervalSeconds` - background SignalR publish cadence.
- `Deployment.*` - install mode, service name, bundle version, install root, config/data/log/update paths, and manifest path surfaced by `/api/health`.
- `Updates.*` - placeholder update status/channel/manifest fields for future dashboard-controlled updates.

The `TracksideTray` section in `Trackside.Tray/appsettings.json` controls tray menu entries and the service base URL.

Tray menu actions support:

- `OpenUrl` with either `Url` or app-relative `Route`.
- `Separator`.
- `Exit` for graceful host shutdown.

## Extension Points

- Add shared-memory parsing behind `IRf2ScoringPayloadParser` and `ILiveSessionSource` without changing API or kiosk contracts.
- Add storage as a separate service behind repositories/workers; do not put SQLite writes in the source reader hot path.
- Add admin controls as new endpoint groups and React routes while keeping `/api/live-session/current` stable for kiosk reconnects.
- Add tray commands by calling service endpoints or Windows service-control operations; do not put backend business logic in the tray process.
- Add update behavior behind `Trackside.Updater` or package scripts; keep file replacement outside `Trackside.Service`.
