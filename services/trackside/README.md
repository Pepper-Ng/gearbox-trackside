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
- `Trackside.Tests` - xUnit tests for fixture contracts, API route stability, CLI aliases, shared-memory parser scaffolding, Phase 1 ordering/highlighting/aliases, and current-snapshot recovery.

## Architecture Choices

- ASP.NET Core Generic Host provides dependency injection, configuration, logging, hosted services, and graceful shutdown.
- The solution uses a light Clean Architecture / Ports and Adapters split: Service composes, Application defines ports, Infrastructure implements adapters, Domain stays pure.
- SignalR is the browser push layer. Clients should load `/api/live-session/current` first, then subscribe to `/hubs/live-session`.
- `ILiveSessionSource` hides whether the current snapshot comes from a fixture, recorded data, or future shared-memory parsing.
- The tray companion uses Windows Forms `NotifyIcon` because it is the standard Windows notification-area API, but it is a separate executable from the service.
- A separate `Trackside.RigAgent` binary exists so future client/rig-side behavior does not get mixed into the central host or browser UI.
- A separate `Trackside.Updater` binary exists so future update application does not require the service to overwrite its own running files.
- Phase 1 keeps the fixture-first boundary: raw leaderboard source channels are normalized by `LeaderboardSnapshotBuilder`, and both fixture and shared-memory sources feed that same contract.
- `SharedMemory` mode has a guarded scoring map reader, parser, dedicated polling loop, auto-discovery for visible PID-suffixed scoring maps, explicit map/PID overrides, stale-read clearing, and scoring update-counter stability checks. It has been live-validated on the local PC for the current leaderboard fields.
- `ReloadingLiveSessionSource` keeps the `ILiveSessionSource` boundary stable while recreating the concrete source when admin-edited source settings change.
- The browser-facing live-session contract exposes the normalized leaderboard/scoring fields used in Phase 1, including session metadata, flag/weather fields, lap distance, driver timing, sectors, gaps, lap progress, and highlight flags. It intentionally does not expose raw map names, decode offsets, or other admin-only diagnostics.
- Phase 2 persistence starts behind `ITracksideStore` in the Application layer. The current service registers a SQLite adapter in Infrastructure, but live publishing, source alias resolution, and admin endpoints depend on the provider-neutral store contract so a later MySQL/PostgreSQL/etc. adapter can replace SQLite without changing those callers.
- Best-lap boards use stored lap records, not aggregate best-lap summaries. Trackside exposes rFactor 2's lap validity field as `ValidLapFlag`; it must be `2` before a lap is eligible for daily/weekly/monthly timing boards. Public boards default to one fastest lap per entered screen name so one driver cannot fill the whole display.
- Prepared session setup lives in the durable store: staff assign screen names to rigs before sessions and may optionally link a recurring-customer driver profile. The setup remains active until staff saves changes or clears it.
- The admin Sessions tab is a stored history browser, not a live-session monitor. It reads persisted sessions and participants from `ITracksideStore`; Include/Exclude updates `count_for_history`, per-row Delete removes bad stored sessions, and Delete Empty Sessions clears placeholder rows that have no participants, no completed laps, or no known track.
- Participant display-name corrections, participant exclusions, and lap invalidations are persisted separately from captured source data, then projected into long-lived derived track-best records used by historical boards.
- Kiosk screens read a backend-configured default display mode from `/api/configuration/client`; admins can change the default mode without editing files.

## Commands

Run from the repository root:

```powershell
dotnet build services\trackside\Trackside.slnx
dotnet test services\trackside\Trackside.slnx
dotnet run --project services\trackside\Trackside.Service -- --console --source fixture --fixture Fixtures\scoring-leaderboard-practice.json
```

Open `http://127.0.0.1:8877` for the packaged/static kiosk shell.
Open `http://127.0.0.1:8877/config` for the admin dashboard. On a new install, the installer should create the first admin user; if no admin store exists, the dashboard shows a first-run setup form. After login, admins can edit source/alias/shared-memory discovery settings, prepare rig/session assignments, browse persisted sessions and participants, correct/exclude participants, invalidate laps, toggle whether a session counts for historical boards, configure the default kiosk display mode, run retention cleanup, create admin users, change passwords, and view advanced service status.

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

The install script verifies the bundle manifest through `Trackside.Updater` before copying files. During a real install, it creates the first admin user store at `data\security\admin-users.json` unless `-SkipAdminBootstrap` is supplied or a store already exists. Passwords are stored as salted PBKDF2-HMAC-SHA256 hashes, never plaintext. For unattended installs, pass `-AdminUsername` and `-AdminPassword` as a `SecureString`.

## Configuration

The `Trackside` section in `Trackside.Service/appsettings.json` controls:

- `Http.ListenUrl` - Kestrel binding URL.
- `Http.PublicBaseUrl` - URL opened by tray actions.
- `Source.Mode` - currently `Fixture`; future modes are `SharedMemory` and `Recorded`.
- `Source.FixturePath` - raw scoring-style or normalized live-session fixture JSON.
- `Source.DriverAliases` - temporary rig-name to display-name aliases, such as `Setup1` to a customer name.
- `Source.SharedMemory.*` - rFactor 2 scoring map name/PID and polling settings used by `Source.Mode = SharedMemory`.
- `Source.SharedMemory.AutoDiscover` - scans configured dedicated-server process names and visible Windows `Section` objects for scoring maps.
- `Source.SharedMemory.DedicatedServerProcessNames` - process-name hints used to probe PID-suffixed maps after Dedicated Server restarts.
- `Source.SharedMemory.MultipleScoringMapPolicy` - defaults to `RequireExplicitSelection`, so multiple simultaneous PID-suffixed scoring maps are reported instead of silently chosen.
- `Source.SharedMemory.Telemetry.Enabled` - enabled by default for `Source.Mode = SharedMemory`; the high-rate telemetry loop feeds generated driver-tracker geometry while scoring remains the leaderboard and lap-validity source.
- `LiveSession.PublishIntervalSeconds` - background SignalR publish cadence.
- `Persistence.Enabled` - enables the durable Phase 2 store.
- `Persistence.DatabasePath` / `Persistence.DatabaseFileName` - optional database location override or file name under the resolved data directory.
- `Persistence.CountSessionsByDefault` - default inclusion flag for newly persisted live-session summaries until staff changes a session in the admin Sessions tab.
- `Persistence.Retention.*` - retention policy targets for detailed lap records, session summaries, long-lived track best records, monthly track periods, and future telemetry samples. A background cleanup worker and admin trigger enforce these windows while preserving derived track-best records by default.
- `Kiosk.DefaultDisplayMode` - default kiosk view for newly opened screens: Monthly, Weekly, Daily, LastSession, or Live.
- `DriverTracker.ClientRefreshHz` - browser-side refresh/redraw rate for `/tracker`, defaulting to 50 Hz. Source freshness still depends on the active rFactor 2 scoring or telemetry source.
- `DriverTracker.GeometryRecordingLaps` - default number of complete lap passes to average before generated track geometry is considered complete. The admin Driver Tracker panel can restart recording per seen track to improve stored geometry.
- `Deployment.*` - install mode, service name, bundle version, install root, config/data/log/update paths, and manifest path. Detailed paths are surfaced through the authenticated admin status endpoint rather than public `/api/health`.
- `Updates.*` - placeholder update status/channel/manifest fields for future dashboard-controlled updates.

Shared-memory streams are projected once and published to registered live-data consumers. The driver-tracker geometry recorder consumes only scoring context and reduced telemetry-position frames, and it persists averaged geometry rather than raw telemetry.

The tray companion includes an `Open Configuration` menu item that opens the service-hosted configuration page.

## Admin Security

- Public pages: kiosk and basic health. Public health intentionally omits paths, source diagnostics, discovery candidates, and admin-store details.
- Admin pages: `/configuration.html` shell plus authenticated admin APIs for source configuration, admin users, and advanced status.
- Admin authentication uses an HttpOnly same-site cookie.
- First admin creation is installer-first for venue installs, with a web first-run fallback only while no admin users exist.
- Later admin users are created from the admin dashboard.
- Admin passwords are hashed with PBKDF2-HMAC-SHA256 using random salts and 210,000 iterations.
- Admin user and source configuration writes use temp-file replacement to avoid partial JSON writes.
- Packaged installs restrict `data\security` ACLs to SYSTEM and Administrators before writing the first admin store.

The `TracksideTray` section in `Trackside.Tray/appsettings.json` controls tray menu entries and the service base URL.
The tray icon overlays a lower-right status dot: red means no shared-memory map connection, blue means a shared-memory map is connected, and green means a shared-memory map is connected with an active session. `StatusRefreshSeconds` and `StatusRequestTimeoutSeconds` control that polling behavior.

Tray menu actions support:

- `OpenUrl` with either `Url` or app-relative `Route`.
- `Separator`.
- `Exit` for graceful host shutdown.

## Extension Points

- Harden shared-memory parsing behind `IRf2ScoringPayloadParser` and `ILiveSessionSource` without changing API or kiosk contracts.
- Keep leaderboard layout components separate from feed/startup helpers so future kiosk graphics can change without changing the source contracts.
- Add storage as a separate service behind repositories/workers; do not put SQLite writes in the source reader hot path.
- Add admin controls as new endpoint groups and React routes while keeping `/api/live-session/current` stable for kiosk reconnects.
- Add tray commands by calling service endpoints or Windows service-control operations; do not put backend business logic in the tray process.
- Add update behavior behind `Trackside.Updater` or package scripts; keep file replacement outside `Trackside.Service`.
